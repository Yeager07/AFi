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
    /// Возвращает последние N операций, отсортированных по дате операции,
    /// затем по дате создания (для одинаковых дат).
    /// </summary>
    public async Task<List<Transaction>> GetRecentTransactionsAsync(int count = 10)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync();
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