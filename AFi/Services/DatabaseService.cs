using AFi.Models;
using SQLite;

namespace AFi.Services;

/// <summary>
/// Сервис для работы с локальной базой данных SQLite.
/// Регистрируется как Singleton в MauiProgram — на всё приложение
/// создаётся одно подключение к БД.
/// </summary>
public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private const string DatabaseFileName = "afi.db3";

    /// <summary>
    /// Путь к файлу БД. AppDataDirectory — это папка приложения на устройстве,
    /// куда мы имеем право писать без разрешений. На Android это что-то вроде
    /// /data/data/com.companyname.afi/files/.
    /// </summary>
    private static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    /// <summary>
    /// Ленивая инициализация: создаёт подключение, таблицы и засеивает
    /// категории при первом вызове. Безопасна при параллельных вызовах
    /// благодаря SemaphoreSlim.
    /// </summary>
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

            await SeedDefaultCategoriesAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Создаёт подключение, если его ещё нет. Вызывается из всех публичных методов.
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (!_initialized)
            await InitializeAsync();

        return _connection!;
    }

    // ==================== Транзакции ====================

    /// <summary>
    /// Добавляет операцию в БД. Возвращает количество вставленных строк (обычно 1).
    /// </summary>
    public async Task<int> AddTransactionAsync(Transaction transaction)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(transaction);
    }

    /// <summary>
    /// Обновляет существующую операцию в БД.
    /// Возвращает количество изменённых строк (обычно 1).
    /// </summary>
    public async Task<int> UpdateTransactionAsync(Transaction transaction)
    {
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(transaction);
    }

    /// <summary>
    /// Удаляет операцию по Id. Возвращает количество удалённых строк.
    /// </summary>
    public async Task<int> DeleteTransactionAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync<Transaction>(id);
    }

    /// <summary>
    /// Возвращает постранично отфильтрованные операции.
    /// categoryId = null означает «без фильтра по категории».
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
        var all = await db.Table<Transaction>().ToListAsync();

        var filtered = ApplyFilters(all, fromInclusive, toInclusive, categoryId, categoryName);

        return filtered
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Возвращает общее число операций за период. Нужно для определения,
    /// показывать ли кнопку «Показать ещё».
    /// </summary>
    public async Task<int> GetTransactionsCountAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null,
        int? categoryId = null,
        string? categoryName = null)
    {
        var db = await GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();
        return ApplyFilters(all, fromInclusive, toInclusive, categoryId, categoryName).Count();
    }
    
    /// <summary>
    /// Общий метод фильтрации по датам — используется и в GetTransactionsAsync,
    /// и в GetTransactionsCountAsync, чтобы логика была в одном месте.
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

        // Приоритет у Id — если задан, фильтруем по нему.
        // Если Id нет, но есть имя — по имени (для удалённых категорий).
        if (categoryId.HasValue)
            result = result.Where(t => t.CategoryId == categoryId.Value);
        else if (!string.IsNullOrEmpty(categoryName))
            result = result.Where(t => t.CategoryName == categoryName);

        return result;
    }

    /// <summary>
    /// Возвращает общее количество операций. Пригодится для отладки
    /// и для Этапа 2 (пагинация).
    /// </summary>
    public async Task<int> GetTransactionCountAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>().CountAsync();
    }

    /// <summary>
    /// Возвращает суммарные показатели (доход, расход) за период
    /// с учётом фильтра по категории.
    /// </summary>
    public async Task<(decimal Income, decimal Expense)> GetSummaryAsync(
    DateTime? fromInclusive = null,
    DateTime? toInclusive = null,
    int? categoryId = null,
    string? categoryName = null)
    {
        var db = await GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();

        var filtered = ApplyFilters(all, fromInclusive, toInclusive, categoryId, categoryName);

        decimal income = 0, expense = 0;
        foreach (var t in filtered)
        {
            if (t.Type == TransactionType.Income) income += t.Amount;
            else expense += t.Amount;
        }

        return (income, expense);
    }

    /// <summary>
    /// Возвращает сумму расходов в разбивке по категориям за период.
    /// Отсортировано по убыванию суммы — для диаграммы и легенды.
    /// </summary>
    public async Task<List<(string CategoryName, decimal Sum)>> GetExpensesByCategoryAsync(
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null)
    {
        var db = await GetConnectionAsync();
        var all = await db.Table<Transaction>().ToListAsync();

        var filtered = ApplyFilters(all, fromInclusive, toInclusive, null)
            .Where(t => t.Type == TransactionType.Expense);

        return filtered
            .GroupBy(t => t.CategoryName)
            .Select(g => (CategoryName: g.Key, Sum: g.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Sum)
            .ToList();
    }

    /// <summary>
    /// Возвращает доходы и расходы по месяцам за период.
    /// Ключ — год и месяц, значение — суммы. Используется для столбчатой диаграммы.
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

    // ==================== Баланс ====================

    /// <summary>
    /// Возвращает текущий баланс: сумма доходов минус сумма расходов.
    /// Начальный баланс пока считается нулевым — редактирование добавим позже.
    /// </summary>
    /// <remarks>
    /// Считаем в C#, а не SQL-запросом с SUM, чтобы избежать потери точности:
    /// sqlite-net сохраняет decimal как REAL (double), и суммирование в SQL
    /// может дать погрешность на копейки. Для финансов это недопустимо.
    /// </remarks>
    public async Task<decimal> GetCurrentBalanceAsync()
    {
        var db = await GetConnectionAsync();

        var all = await db.Table<Transaction>().ToListAsync();

        decimal income = 0;
        decimal expense = 0;

        foreach (var t in all)
        {
            if (t.Type == TransactionType.Income)
                income += t.Amount;
            else
                expense += t.Amount;
        }

        return income - expense;
    }

    // ==================== Категории ====================

    /// <summary>
    /// Возвращает все категории заданного типа (доходы или расходы).
    /// </summary>
    public async Task<List<Category>> GetCategoriesAsync(TransactionType type)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Category>()
            .Where(c => c.Type == type)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Возвращает все категории без фильтра. Пригодится на Этапе 2.
    /// </summary>
    public async Task<List<Category>> GetAllCategoriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Category>()
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Добавляет новую категорию. Возвращает количество вставленных строк.
    /// </summary>
    public async Task<int> AddCategoryAsync(Category category)
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(category);
    }

    /// <summary>
    /// Возвращает количество операций, привязанных к указанной категории.
    /// Используется при удалении категории — показать пользователю предупреждение.
    /// </summary>
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
    ///
    /// Денормализованное CategoryName в операциях сохраняется —
    /// историческая запись остаётся понятной для пользователя.
    /// </summary>
    public async Task DeleteCategoryAsync(int categoryId)
    {
        var db = await GetConnectionAsync();

        // Отвязываем операции одной командой — быстрее и атомарнее,
        // чем вытягивать список и обновлять по одной.
        await db.ExecuteAsync(
            "UPDATE transactions SET CategoryId = NULL WHERE CategoryId = ?",
            categoryId);

        await db.DeleteAsync<Category>(categoryId);
    }

    // ==================== Засев категорий ====================

    /// <summary>
    /// Засеивает предустановленные категории. Вызывается только один раз —
    /// если в таблице уже есть хоть одна категория, ничего не делает.
    /// </summary>
    private async Task SeedDefaultCategoriesAsync()
    {
        var db = _connection!;
        var existing = await db.Table<Category>().CountAsync();
        if (existing > 0) return;

        var defaults = new List<Category>
        {
            // ============ Расходы ============
            new() { Name = "Аренда",                  Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Закупки (сырьё)",         Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Налоги",                  Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Коммунальные услуги",     Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "АЗС",                     Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Транспорт",               Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Кафе и рестораны",        Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Дом и ремонт",            Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Одежда и обувь",          Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Подписки",                Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Цветы",                   Type = TransactionType.Expense, IsDefault = true },
            new() { Name = "Прочее",                  Type = TransactionType.Expense, IsDefault = true },

            // ============ Доходы ============
            new() { Name = "Выручка",                 Type = TransactionType.Income,  IsDefault = true },
            new() { Name = "Зарплата",                Type = TransactionType.Income,  IsDefault = true },
            new() { Name = "Прочие поступления",      Type = TransactionType.Income,  IsDefault = true },
        };

        await db.InsertAllAsync(defaults);
    }
}