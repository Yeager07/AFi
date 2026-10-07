using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

/// <summary>
/// ViewModel главного экрана: форма ввода операции + список последних операций.
/// Наследуется от ObservableObject — это даёт INotifyPropertyChanged,
/// а [ObservableProperty] из CommunityToolkit генерирует свойства на этапе компиляции.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    // ==================== Состояние формы ====================

    /// <summary>
    /// Сумма как строка. Entry в MAUI возвращает string,
    /// парсим в decimal уже в SaveAsync с нормальной валидацией.
    /// </summary>
    [ObservableProperty]
    private string _amountText = string.Empty;

    /// <summary>
    /// Индекс выбранного типа в Picker'е. 0 — Расход, 1 — Доход.
    /// Расход стоит первым, потому что 90% операций у микробизнеса — расходы.
    /// </summary>
    [ObservableProperty]
    private int _selectedTypeIndex = 0;

    /// <summary>
    /// Выбранная категория. null, пока пользователь не выбрал.
    /// </summary>
    [ObservableProperty]
    private Category? _selectedCategory;

    /// <summary>
    /// Дата операции. По умолчанию — сегодня.
    /// </summary>
    [ObservableProperty]
    private DateTime _date = DateTime.Today;

    /// <summary>
    /// Комментарий. Пустая строка в форме — сохраняем как null.
    /// </summary>
    [ObservableProperty]
    private string _note = string.Empty;

    /// <summary>
    /// Сообщение для пользователя: «Операция сохранена», «Введите сумму» и т.д.
    /// Пустая строка — ничего не показываем.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Флаг «идёт сохранение» — блокируем кнопку, чтобы не было двойных кликов.
    /// </summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Текущий баланс: сумма доходов минус сумма расходов.
    /// Обновляется после каждой операции и при загрузке экрана.
    /// </summary>
    [ObservableProperty]
    private decimal _currentBalance;

    // ==================== Списки ====================

    /// <summary>
    /// Опции для Picker типа операции. Соответствуют индексам 0/1
    /// в SelectedTypeIndex.
    /// </summary>
    public ObservableCollection<string> TypeOptions { get; } = new()
    {
        "Расход",
        "Доход"
    };

    /// <summary>
    /// Категории, доступные для текущего выбранного типа.
    /// Перезаполняется при смене типа или после загрузки из БД.
    /// </summary>
    public ObservableCollection<Category> AvailableCategories { get; } = new();

    /// <summary>
    /// Последние N операций для отображения.
    /// </summary>
    public ObservableCollection<Transaction> RecentTransactions { get; } = new();

    /// <summary>
    /// Маппинг индекса Picker в тип операции.
    /// </summary>
    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    /// <summary>
    /// Отрицательный ли баланс. Используется в XAML для выбора цвета текста.
    /// </summary>
    public bool IsBalanceNegative => CurrentBalance < 0;

    public MainViewModel(DatabaseService db)
    {
        _db = db;
    }

    // ==================== Реакция на смену типа ====================

    /// <summary>
    /// CommunityToolkit генерирует вызов этого метода автоматически,
    /// когда меняется SelectedTypeIndex. Мы используем его, чтобы
    /// перезагрузить список категорий под новый тип.
    /// </summary>
    partial void OnSelectedTypeIndexChanged(int value)
    {
        // Fire-and-forget: не блокируем UI, ошибки ловим внутри метода.
        _ = ReloadCategoriesAsync();
    }

    /// <summary>
    /// Генерируется CommunityToolkit при изменении CurrentBalance.
    /// Уведомляем UI, что IsBalanceNegative тоже мог измениться.
    /// </summary>
    partial void OnCurrentBalanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(IsBalanceNegative));
    }

    // ==================== Команды ====================

    /// <summary>
    /// Загружает категории и последние операции при открытии экрана.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _db.InitializeAsync();
            await ReloadCategoriesAsync();
            await ReloadTransactionsAsync();
            await ReloadBalanceAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Сохраняет операцию: валидирует форму, пишет в БД, очищает форму,
    /// обновляет список последних операций.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        // ---------- Валидация ----------
        if (string.IsNullOrWhiteSpace(AmountText))
        {
            StatusMessage = "Введите сумму";
            return;
        }

        // Парсим с учётом локали. Replace(',', '.') на случай,
        // если пользователь ввёл запятую как десятичный разделитель.
        var normalized = AmountText.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var amount))
        {
            StatusMessage = "Сумма должна быть числом";
            return;
        }

        if (amount <= 0)
        {
            StatusMessage = "Сумма должна быть больше нуля";
            return;
        }

        if (SelectedCategory is null)
        {
            StatusMessage = "Выберите категорию";
            return;
        }

        IsBusy = true;
        try
        {
            var transaction = new Transaction
            {
                Amount = amount,
                Type = CurrentType,
                CategoryId = SelectedCategory.Id,
                CategoryName = SelectedCategory.Name,
                Date = Date,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                CreatedAt = DateTime.Now
            };

            await _db.AddTransactionAsync(transaction);

            ResetForm();
            await ReloadTransactionsAsync();
            await ReloadBalanceAsync();

            StatusMessage = "Операция сохранена";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ==================== Вспомогательные методы ====================

    /// <summary>
    /// Загружает категории выбранного типа и восстанавливает выбор,
    /// если категория с тем же именем осталась в новом списке.
    /// </summary>
    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var previousName = SelectedCategory?.Name;

            var categories = await _db.GetCategoriesAsync(CurrentType);

            AvailableCategories.Clear();
            foreach (var c in categories)
                AvailableCategories.Add(c);

            // Восстанавливаем выбор по имени (если категория того же типа
            // была выбрана и осталась доступной). Иначе — первая в списке.
            SelectedCategory =
                AvailableCategories.FirstOrDefault(c => c.Name == previousName)
                ?? AvailableCategories.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки категорий: {ex.Message}";
        }
    }

    /// <summary>
    /// Перезагружает список последних операций (по умолчанию 10).
    /// </summary>
    private async Task ReloadTransactionsAsync()
    {
        try
        {
            var transactions = await _db.GetRecentTransactionsAsync(10);

            RecentTransactions.Clear();
            foreach (var t in transactions)
                RecentTransactions.Add(t);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки операций: {ex.Message}";
        }
    }

    /// <summary>
    /// Очищает форму после успешного сохранения.
    /// Тип, дата и категория остаются — это удобно, если пользователь
    /// вносит несколько операций подряд одного типа.
    /// </summary>
    private void ResetForm()
    {
        AmountText = string.Empty;
        Note = string.Empty;
        // Date и SelectedTypeIndex оставляем как есть.
        // SelectedCategory тоже оставляем — если вводится много трат одной категории.
    }

    /// <summary>
    /// Пересчитывает и обновляет текущий баланс.
    /// </summary>
    private async Task ReloadBalanceAsync()
    {
        try
        {
            CurrentBalance = await _db.GetCurrentBalanceAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки баланса: {ex.Message}";
        }
    }
}