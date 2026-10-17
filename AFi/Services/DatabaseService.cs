using AFi.Models;
using SQLite;

namespace AFi.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    // ==================== Кэш категорий ====================
    // Категории меняются редко (несколько раз за всё время использования),
    // но читаются при каждой загрузке Главной, Истории, Настроек, Статистики.
    // Держим их в памяти, инвалидируем при добавлении/удалении.
    private List<Category>? _categoriesCache;
    private readonly SemaphoreSlim _categoriesLock = new(1, 1);

    private const string DatabaseFileName = "afi.db3";
    private const string IconsMigrationKey = "migration_icons_v1_done";

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

        await db.ExecuteAsync(
            "DELETE FROM transaction_items WHERE TransactionId = ?",
            id);

        return await db.DeleteAsync<Transaction>(id);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync();
    }

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

    // ==================== Категории (с кэшем) ====================

    public async Task<List<Category>> GetCategoriesAsync(TransactionType type)
    {
        var all = await EnsureCategoriesCacheAsync();
        return all.Where(c => c.Type == type).ToList();
    }

    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        var all = await EnsureCategoriesCacheAsync();
        return new List<Category>(all);
    }

    public async Task<int> AddCategoryAsync(Category category)
    {
        var db = await GetConnectionAsync();
        var result = await db.InsertAsync(category);

        _categoriesCache?.Add(category);

        return result;
    }

    public async Task<int> CountTransactionsByCategoryAsync(int categoryId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => t.CategoryId == categoryId)
            .CountAsync();
    }

    public async Task DeleteCategoryAsync(int categoryId)
    {
        var db = await GetConnectionAsync();

        await db.ExecuteAsync(
            "UPDATE transactions SET CategoryId = NULL WHERE CategoryId = ?",
            categoryId);

        await db.DeleteAsync<Category>(categoryId);

        _categoriesCache?.RemoveAll(c => c.Id == categoryId);
    }

    private async Task<List<Category>> EnsureCategoriesCacheAsync()
    {
        if (_categoriesCache is not null)
            return _categoriesCache;

        await _categoriesLock.WaitAsync();
        try
        {
            if (_categoriesCache is not null)
                return _categoriesCache;

            var db = await GetConnectionAsync();
            _categoriesCache = await db.Table<Category>()
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToListAsync();

            return _categoriesCache;
        }
        finally
        {
            _categoriesLock.Release();
        }
    }

    // ==================== Засев и миграции ====================

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
    /// Обновляет иконки у категорий и операций, где поле Icon пустое.
    /// Выполняется один раз — после успешной миграции ставится флаг
    /// в Preferences, чтобы не читать всю таблицу при каждом старте.
    /// </summary>
    private async Task MigrateIconsAsync()
    {
        if (Preferences.Get(IconsMigrationKey, false))
            return;

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

        // Только те категории, у которых иконка пустая
        var categoriesWithoutIcon = await db.QueryAsync<Category>(
            "SELECT * FROM categories WHERE Icon IS NULL OR Icon = ''");

        if (categoriesWithoutIcon.Count > 0)
        {
            foreach (var c in categoriesWithoutIcon)
            {
                c.Icon = defaultIcons.TryGetValue(c.Name, out var icon) ? icon : "👤";
            }
            await db.UpdateAllAsync(categoriesWithoutIcon);
        }

        // Только те операции, у которых иконка пустая
        var transactionsWithoutIcon = await db.QueryAsync<Transaction>(
            "SELECT * FROM transactions WHERE CategoryIcon IS NULL OR CategoryIcon = ''");

        if (transactionsWithoutIcon.Count > 0)
        {
            var allCategories = await db.Table<Category>().ToListAsync();
            var iconByName = allCategories.ToDictionary(
                c => c.Name, c => c.Icon, StringComparer.OrdinalIgnoreCase);

            foreach (var t in transactionsWithoutIcon)
            {
                t.CategoryIcon = iconByName.TryGetValue(t.CategoryName, out var icon)
                    ? icon
                    : defaultIcons.TryGetValue(t.CategoryName, out var defIcon)
                        ? defIcon
                        : "❓";
            }
            await db.UpdateAllAsync(transactionsWithoutIcon);
        }

        Preferences.Set(IconsMigrationKey, true);
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

    // ==================== Статистика ====================

    public async Task<List<(string CategoryName, string Icon, decimal Sum)>> GetExpensesByCategoryAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null)
    {
        var db = await GetConnectionAsync();

        var (whereClause, parameters) = BuildWhereClause(
            fromInclusive, toInclusive, null, null);

        var extraClause = string.IsNullOrEmpty(whereClause)
            ? "WHERE Type = ?"
            : whereClause + " AND Type = ?";

        var sql = $@"
            SELECT 
                CategoryName,
                MAX(CategoryIcon) AS Icon,
                SUM(Amount) AS Sum
            FROM transactions
            {extraClause}
            GROUP BY CategoryName
            ORDER BY SUM(Amount) DESC";

        parameters.Add((int)TransactionType.Expense);

        var rows = await db.QueryAsync<ExpenseCategoryRow>(sql, parameters.ToArray());

        return rows
            .Select(r => (r.CategoryName, r.Icon ?? string.Empty, (decimal)r.Sum))
            .ToList();
    }

    public async Task<List<(DateTime Month, decimal Income, decimal Expense)>> GetMonthlyTotalsAsync(
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        var db = await GetConnectionAsync();

        var (whereClause, parameters) = BuildWhereClause(
            fromInclusive, toInclusive, null, null);

        const string ticksToUnix = "(Date - 621355968000000000) / 10000000";

        var sql = $@"
            SELECT 
                CAST(strftime('%Y', datetime({ticksToUnix}, 'unixepoch')) AS INTEGER) AS Year,
                CAST(strftime('%m', datetime({ticksToUnix}, 'unixepoch')) AS INTEGER) AS Month,
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN Type = ? THEN Amount ELSE 0 END), 0) AS Expense
            FROM transactions
            {whereClause}
            GROUP BY Year, Month
            ORDER BY Year, Month";

        var allParams = new List<object>
        {
            (int)TransactionType.Income,
            (int)TransactionType.Expense
        };
        allParams.AddRange(parameters);

        var rows = await db.QueryAsync<MonthlyTotalsRow>(sql, allParams.ToArray());

        return rows
            .Select(r => (
                new DateTime(r.Year, r.Month, 1),
                (decimal)r.Income,
                (decimal)r.Expense))
            .ToList();
    }

    // ==================== Вспомогательные ====================

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

    // ==================== DTO для SQL-запросов ====================

    private class SummaryResult
    {
        public double Income { get; set; }
        public double Expense { get; set; }
    }

    private class ExpenseCategoryRow
    {
        public string CategoryName { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public double Sum { get; set; }
    }

    private class MonthlyTotalsRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public double Income { get; set; }
        public double Expense { get; set; }
    }
}