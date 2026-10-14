using System.Runtime.InteropServices;
using AFi.Models;
using SQLite;

namespace AFi.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private const string DatabaseFileName = "afi.db3";

    private static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    // ==================== Инициализация ====================

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            _connection = new SQLiteAsyncConnection(
                DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await _connection.CreateTableAsync<Category>();
            await _connection.CreateTableAsync<Transaction>();
            await _connection.CreateTableAsync<TransactionItem>();
            await _connection.CreateTableAsync<RecurringPayment>();

            await SeedDefaultCategoriesAsync();
            await MigrateIconsAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (!_initialized)
            await InitializeAsync();

        return _connection!;
    }

    // ==================== Транзакции ====================

    public async Task<int> AddTransactionAsync(Transaction transaction)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(transaction);
    }

    public async Task<int> UpdateTransactionAsync(Transaction transaction)
    {
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(transaction);
    }

    public async Task<int> DeleteTransactionAsync(int id)
    {
        var db = await GetConnectionAsync();

        // Сначала удаляем позиции чека
        await db.ExecuteAsync(
            "DELETE FROM transaction_items WHERE TransactionId = ?",
            id);

        // Потом саму операцию
        return await db.DeleteAsync<Transaction>(id);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Возвращает постранично отфильтрованные операции.
    /// Фильтрация и пагинация — в SQL, чтобы не тянуть всю таблицу в память.
    /// </summary>
    public async Task<List<Transaction>> GetTransactionsAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null,
        int? categoryId = null,
        int limit = 100,
        int offset = 0,
        string? categoryName = null)
    {
        var db = await GetConnectionAsync();

        var (whereClause, parameters) = BuildWhereClause(
            fromInclusive, toInclusive, categoryId, categoryName);

        var sql = $@"
            SELECT * FROM transactions
            {whereClause}
            ORDER BY Date DESC, CreatedAt DESC
            LIMIT ? OFFSET ?";

        parameters.Add(limit);
        parameters.Add(offset);

        return await db.QueryAsync<Transaction>(sql, parameters.ToArray());
    }

    public async Task<int> GetTransactionsCountAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null,
        int? categoryId = null,
        string? categoryName = null)
    {
        var db = await GetConnectionAsync();

        var (whereClause, parameters) = BuildWhereClause(
            fromInclusive, toInclusive, categoryId, categoryName);

        var sql = $"SELECT COUNT(*) FROM transactions {whereClause}";

        return await db.ExecuteScalarAsync<int>(sql, parameters.ToArray());
    }

    /// <summary>
    /// Возвращает суммарные показатели (доход, расход) за период.
    /// Считается в SQL, данные не тянутся в память.
    /// </summary>
    public async Task<(decimal Income, decimal Expense)> GetSummaryAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null,
        int? categoryId = null,
        string? categoryName = null)
    {
        var db = await GetConnectionAsync();

        var (whereClause, parameters) = BuildWhereClause(
            fromInclusive, toInclusive, categoryId, categoryName);

        var sql = $@"
            SELECT 
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Expense
            FROM transactions
            {whereClause}";

        var allParams = new List<object>
        {
            (int)TransactionType.Income,
            (int)TransactionType.Expense
        };
        allParams.AddRange(parameters);

        var result = await db.QueryAsync<SummaryResult>(sql, allParams.ToArray());

        if (result.Count == 0)
            return (0m, 0m);

        return ((decimal)result[0].Income, (decimal)result[0].Expense);
    }

    /// <summary>
    /// Возвращает текущий баланс по всем операциям (без начального).
    /// Считается в SQL.
    /// </summary>
    public async Task<decimal> GetCurrentBalanceAsync()
    {
        var db = await GetConnectionAsync();

        const string sql = @"
            SELECT 
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Expense
            FROM transactions";

        var result = await db.QueryAsync<SummaryResult>(
            sql, (int)TransactionType.Income, (int)TransactionType.Expense);

        if (result.Count == 0)
            return 0m;

        return (decimal)result[0].Income - (decimal)result[0].Expense;
    }

    // ==================== Категории ====================

    public async Task<List<Category>> GetCategoriesAsync(TransactionType type)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Category>()
            .Where(c => c.Type == type)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Category>()
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<int> AddCategoryAsync(Category category)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(category);
    }

    public async Task<int> CountTransactionsByCategoryAsync(int categoryId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => t.CategoryId == categoryId)
            .CountAsync();
    }

    /// <summary>
    /// Удаляет категорию. Перед удалением отвязывает все операции,
    /// у которых CategoryId == удаляемой категории (ставит NULL).
    /// Денормализованное CategoryName в операциях сохраняется.
    /// </summary>
    public async Task DeleteCategoryAsync(int categoryId)
    {
        var db = await GetConnectionAsync();

        await db.ExecuteAsync(
            "UPDATE transactions SET CategoryId = NULL WHERE CategoryId = ?",
            categoryId);

        await db.DeleteAsync<Category>(categoryId);
    }

    // ==================== Засев категорий ====================

    private async Task SeedDefaultCategoriesAsync()
    {
        var db = _connection!;
        var existing = await db.Table<Category>().CountAsync();
        if (existing > 0) return;

        var defaults = new List<Category>
        {
            new() { Name = "Аренда",              Type = TransactionType.Expense, IsDefault = true, Icon = "🏠" },
            new() { Name = "Закупки (сырьё)",     Type = TransactionType.Expense, IsDefault = true, Icon = "📦" },
            new() { Name = "Налоги",              Type = TransactionType.Expense, IsDefault = true, Icon = "📄" },
            new() { Name = "Коммунальные услуги", Type = TransactionType.Expense, IsDefault = true, Icon = "💡" },
            new() { Name = "АЗС",                 Type = TransactionType.Expense, IsDefault = true, Icon = "⛽" },
            new() { Name = "Транспорт",           Type = TransactionType.Expense, IsDefault = true, Icon = "🚗" },
            new() { Name = "Кафе и рестораны",    Type = TransactionType.Expense, IsDefault = true, Icon = "🍽️" },
            new() { Name = "Дом и ремонт",        Type = TransactionType.Expense, IsDefault = true, Icon = "🔨" },
            new() { Name = "Одежда и обувь",      Type = TransactionType.Expense, IsDefault = true, Icon = "👕" },
            new() { Name = "Подписки",            Type = TransactionType.Expense, IsDefault = true, Icon = "📱" },
            new() { Name = "Цветы",               Type = TransactionType.Expense, IsDefault = true, Icon = "💐" },
            new() { Name = "Прочее",              Type = TransactionType.Expense, IsDefault = true, Icon = "❓" },

            new() { Name = "Выручка",             Type = TransactionType.Income,  IsDefault = true, Icon = "💰" },
            new() { Name = "Зарплата",            Type = TransactionType.Income,  IsDefault = true, Icon = "💵" },
            new() { Name = "Прочие поступления",  Type = TransactionType.Income,  IsDefault = true, Icon = "💸" },
        };

        await db.InsertAllAsync(defaults);
    }

    /// <summary>
    /// Миграция иконок для существующих БД, где колонка Icon ещё NULL/пустая.
    /// </summary>
    private async Task MigrateIconsAsync()
    {
        var db = _connection!;

        var defaultIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Аренда"] = "🏠",
            ["Закупки (сырьё)"] = "📦",
            ["Налоги"] = "📄",
            ["Коммунальные услуги"] = "💡",
            ["АЗС"] = "⛽",
            ["Транспорт"] = "🚗",
            ["Кафе и рестораны"] = "🍽️",
            ["Дом и ремонт"] = "🔨",
            ["Одежда и обувь"] = "👕",
            ["Подписки"] = "📱",
            ["Цветы"] = "💐",
            ["Прочее"] = "❓",
            ["Выручка"] = "💰",
            ["Зарплата"] = "💵",
            ["Прочие поступления"] = "💸",
        };

        // 1. Категории — заполняем пустые иконки
        var categories = await db.Table<Category>().ToListAsync();
        var categoriesToUpdate = new List<Category>();

        foreach (var c in categories)
        {
            if (!string.IsNullOrEmpty(c.Icon)) continue;

            c.Icon = defaultIcons.TryGetValue(c.Name, out var icon)
                ? icon
                : "👤";

            categoriesToUpdate.Add(c);
        }

        if (categoriesToUpdate.Count > 0)
            await db.UpdateAllAsync(categoriesToUpdate);

        // 2. Транзакции — копируем иконку по имени категории
        var transactions = await db.Table<Transaction>().ToListAsync();
        var transactionsToUpdate = new List<Transaction>();

        var iconByName = categories.ToDictionary(c => c.Name, c => c.Icon, StringComparer.OrdinalIgnoreCase);

        foreach (var t in transactions)
        {
            if (!string.IsNullOrEmpty(t.CategoryIcon)) continue;

            t.CategoryIcon = iconByName.TryGetValue(t.CategoryName, out var icon)
                ? icon
                : defaultIcons.TryGetValue(t.CategoryName, out var defIcon)
                    ? defIcon
                    : "❓";

            transactionsToUpdate.Add(t);
        }

        if (transactionsToUpdate.Count > 0)
            await db.UpdateAllAsync(transactionsToUpdate);
    }

    // ==================== Позиции чека ====================

    public async Task<List<TransactionItem>> GetItemsForTransactionAsync(int transactionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<TransactionItem>()
            .Where(i => i.TransactionId == transactionId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Возвращает позиции сразу для нескольких операций за один запрос.
    /// </summary>
    public async Task<Dictionary<int, List<TransactionItem>>> GetItemsForTransactionsAsync(
        List<int> transactionIds)
    {
        var result = new Dictionary<int, List<TransactionItem>>();

        if (transactionIds.Count == 0)
            return result;

        var db = await GetConnectionAsync();

        var placeholders = string.Join(",", transactionIds.Select(_ => "?"));
        var sql = $"SELECT * FROM transaction_items WHERE TransactionId IN ({placeholders}) " +
                  $"ORDER BY TransactionId, SortOrder";

        var items = await db.QueryAsync<TransactionItem>(sql, transactionIds.Cast<object>().ToArray());

        foreach (var item in items)
        {
            if (!result.TryGetValue(item.TransactionId, out var list))
            {
                list = new List<TransactionItem>();
                result[item.TransactionId] = list;
            }
            list.Add(item);
        }

        return result;
    }

    public async Task ReplaceItemsForTransactionAsync(int transactionId, List<TransactionItem> items)
    {
        var db = await GetConnectionAsync();

        await db.ExecuteAsync(
            "DELETE FROM transaction_items WHERE TransactionId = ?",
            transactionId);

        if (items.Count == 0)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            items[i].Id = 0;
            items[i].TransactionId = transactionId;
            items[i].SortOrder = i;
        }

        await db.InsertAllAsync(items);
    }

    public async Task DeleteItemsForTransactionAsync(int transactionId)
    {
        var db = await GetConnectionAsync();
        await db.ExecuteAsync(
            "DELETE FROM transaction_items WHERE TransactionId = ?",
            transactionId);
    }

    public async Task<int> GetItemsCountForTransactionAsync(int transactionId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<TransactionItem>()
            .Where(i => i.TransactionId == transactionId)
            .CountAsync();
    }

    // ==================== Регулярные платежи ====================

    public async Task<List<RecurringPayment>> GetRecurringPaymentsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecurringPayment>()
            .OrderBy(p => p.DayOfMonth)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<List<RecurringPayment>> GetEnabledRecurringPaymentsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<RecurringPayment>()
            .Where(p => p.IsEnabled)
            .ToListAsync();
    }

    public async Task<int> AddRecurringPaymentAsync(RecurringPayment payment)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(payment);
    }

    public async Task<int> UpdateRecurringPaymentAsync(RecurringPayment payment)
    {
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(payment);
    }

    public async Task<int> DeleteRecurringPaymentAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync<RecurringPayment>(id);
    }

    // ==================== Статистика (пока по старинке, оптимизируем далее) ====================

    /// <summary>
    /// Возвращает сумму расходов в разбивке по категориям за период.
    /// </summary>
    public async Task<List<(string CategoryName, string Icon, decimal Sum)>> GetExpensesByCategoryAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null)
    {
        var db = await GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();

        var filtered = ApplyFilters(all, fromInclusive, toInclusive, null)
            .Where(t => t.Type == TransactionType.Expense);

        return filtered
            .GroupBy(t => t.CategoryName)
            .Select(g => (
                CategoryName: g.Key,
                Icon: g.First().CategoryIcon ?? string.Empty,
                Sum: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Sum)
            .ToList();
    }

    /// <summary>
    /// Возвращает доходы и расходы по месяцам за период.
    /// </summary>
    public async Task<List<(DateTime Month, decimal Income, decimal Expense)>> GetMonthlyTotalsAsync(
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        var db = await GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();

        var filtered = ApplyFilters(all, fromInclusive, toInclusive, null);

        var grouped = filtered
            .GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
            .Select(g => (
                Month: g.Key,
                Income: g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                Expense: g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)))
            .OrderBy(x => x.Month)
            .ToList();

        return grouped;
    }

    // ==================== Вспомогательные ====================

    /// <summary>
    /// Собирает WHERE-условие и параметры для SQL-запросов по транзакциям.
    /// Если фильтров нет — clause пустой.
    /// </summary>
    private static (string WhereClause, List<object> Parameters) BuildWhereClause(
        DateTime? fromInclusive,
        DateTime? toInclusive,
        int? categoryId,
        string? categoryName)
    {
        var clauses = new List<string>();
        var parameters = new List<object>();

        if (fromInclusive.HasValue)
        {
            clauses.Add("Date >= ?");
            parameters.Add(fromInclusive.Value.Date.Ticks);
        }

        if (toInclusive.HasValue)
        {
            clauses.Add("Date <= ?");
            parameters.Add(toInclusive.Value.Date.AddDays(1).AddTicks(-1).Ticks);
        }

        if (categoryId.HasValue)
        {
            clauses.Add("CategoryId = ?");
            parameters.Add(categoryId.Value);
        }
        else if (!string.IsNullOrEmpty(categoryName))
        {
            clauses.Add("CategoryName = ?");
            parameters.Add(categoryName);
        }

        var clause = clauses.Count > 0
            ? "WHERE " + string.Join(" AND ", clauses)
            : string.Empty;

        return (clause, parameters);
    }

    /// <summary>
    /// Фильтрация в C# — оставлена для методов статистики,
    /// которые будут переписаны на SQL в следующем шаге.
    /// </summary>
    private static IEnumerable<Transaction> ApplyFilters(
        IEnumerable<Transaction> source,
        DateTime? fromInclusive,
        DateTime? toInclusive,
        int? categoryId,
        string? categoryName = null)
    {
        var result = source;

        if (fromInclusive.HasValue)
            result = result.Where(t => t.Date.Date >= fromInclusive.Value.Date);
        if (toInclusive.HasValue)
            result = result.Where(t => t.Date.Date <= toInclusive.Value.Date);

        if (categoryId.HasValue)
            result = result.Where(t => t.CategoryId == categoryId.Value);
        else if (!string.IsNullOrEmpty(categoryName))
            result = result.Where(t => t.CategoryName == categoryName);

        return result;
    }

    /// <summary>
    /// Вспомогательная модель для чтения SUM из SQL-запроса.
    /// </summary>
    private class SummaryResult
    {
        public double Income { get; set; }
        public double Expense { get; set; }
    }
}