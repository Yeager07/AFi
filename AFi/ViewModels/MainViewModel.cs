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
    private readonly IDialogService _dialogs;

    // ==================== Состояние формы ====================

    private int? _editingTransactionId;
    private Transaction? _editingTransaction;   // ← храним целиком

    private bool _suppressTypeChangeReload;

    /// <summary>
    /// true, если мы редактируем существующую операцию, false — добавляем новую.
    /// </summary>
    public bool IsEditing => _editingTransactionId.HasValue;

    /// <summary>
    /// Заголовок формы — меняется в зависимости от режима.
    /// </summary>
    public string FormTitle => IsEditing ? "Редактирование" : "Новая операция";

    /// <summary>
    /// Текст кнопки сохранения.
    /// </summary>
    public string SubmitButtonText => IsEditing ? "Обновить" : "Сохранить";
    
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
    /// Верхняя граница для DatePicker — сегодня. Запрещает ввод
    /// будущих операций. Обновляется при загрузке экрана на случай,
    /// если приложение работает через полночь.
    /// </summary>
    [ObservableProperty]
    private DateTime _maxDate = DateTime.Today;

    /// <summary>
    /// Начало кастомного периода. Используется только при выборе «Свой период».
    /// По умолчанию — первое число текущего месяца.
    /// </summary>
    [ObservableProperty]
    private DateTime _customFromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    /// <summary>
    /// Конец кастомного периода. По умолчанию — сегодня.
    /// </summary>
    [ObservableProperty]
    private DateTime _customToDate = DateTime.Today;

    /// <summary>
    /// true, если выбран фильтр «Свой период» — по этому флагу
    /// показывается блок с двумя DatePicker и кнопкой «Применить».
    /// </summary>
    public bool IsCustomRangeSelected => SelectedFilterIndex == 9;

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
    /// Выбранный фильтр периода. По умолчанию — «Текущий месяц» (индекс 4).
    /// </summary>
    [ObservableProperty]
    private int _selectedFilterIndex = 4;

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
    /// Список вариантов периода для Picker'а.
    /// </summary>
    public ObservableCollection<string> FilterOptions { get; } = new()
    {
        "Сегодня",
        "Вчера",
        "Эта неделя",
        "Прошлая неделя",
        "Текущий месяц",
        "Прошлый месяц",
        "Последние 3 месяца",
        "Последние 12 месяцев",
        "Все время",
        "Свой период",
    };

    /// <summary>
    /// Маппинг индекса Picker в тип операции.
    /// </summary>
    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    /// <summary>
    /// Отрицательный ли баланс. Используется в XAML для выбора цвета текста.
    /// </summary>
    public bool IsBalanceNegative => CurrentBalance < 0;

    public MainViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
    }

    // ==================== Реакция на смену типа ====================

    /// <summary>
    /// CommunityToolkit генерирует вызов этого метода автоматически,
    /// когда меняется SelectedTypeIndex. Мы используем его, чтобы
    /// перезагрузить список категорий под новый тип.
    /// </summary>
    partial void OnSelectedTypeIndexChanged(int value)
    {
        if (_suppressTypeChangeReload) return;
        _ = ReloadCategoriesAsync();
    }

    partial void OnSelectedFilterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomRangeSelected));

        // Для кастомного периода не перезагружаем сразу — пользователь
        // сначала выставит обе даты и нажмёт «Применить».
        if (value == 9) return;

        _ = ReloadTransactionsAsync();
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
            MaxDate = DateTime.Today;
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

        // Сумма и тип валидируются только для новых операций.
        // При редактировании они неизменяемы и берутся из исходной записи.
        decimal amount;
        if (_editingTransaction is not null)
        {
            amount = _editingTransaction.Amount;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(AmountText))
            {
                StatusMessage = "Введите сумму";
                return;
            }

            var normalized = AmountText.Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Number,
                                CultureInfo.InvariantCulture, out amount))
            {
                StatusMessage = "Сумма должна быть числом";
                return;
            }

            if (amount <= 0)
            {
                StatusMessage = "Сумма должна быть больше нуля";
                return;
            }
        }

        if (SelectedCategory is null)
        {
            StatusMessage = "Выберите категорию";
            return;
        }

        if (Date.Date > DateTime.Today)
        {
            StatusMessage = "Дата операции не может быть в будущем";
            return;
        }

        try
        {
            if (_editingTransaction is not null)
            {
                // Режим редактирования — обновляем только «метаданные»:
                // категорию, дату, комментарий. Amount, Type и CreatedAt
                // остаются исходными (immutable core).
                _editingTransaction.CategoryId = SelectedCategory.Id;
                _editingTransaction.CategoryName = SelectedCategory.Name;
                _editingTransaction.Date = Date;
                _editingTransaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

                await _db.UpdateTransactionAsync(_editingTransaction);

                _editingTransactionId = null;
                _editingTransaction = null;
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(SubmitButtonText));

                StatusMessage = "Операция обновлена";
            }
            else
            {
                // Режим добавления — вставляем новую
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
                StatusMessage = "Операция сохранена";
            }

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

    /// <summary>
    /// Входит в режим редактирования существующей операции.
    /// Заполняет форму её данными.
    /// </summary>
    [RelayCommand]
    private async Task StartEditAsync(Transaction? transaction)
    {
        if (transaction is null) return;
        
        _editingTransaction = transaction;

        _editingTransactionId = transaction.Id;

        // Меняем тип, не триггеря авто-перезагрузку категорий,
        // чтобы самим выстроить порядок: сначала категории, потом выбор.
        _suppressTypeChangeReload = true;
        SelectedTypeIndex = transaction.Type == TransactionType.Income ? 1 : 0;
        _suppressTypeChangeReload = false;

        // Загружаем категории под нужный тип
        await ReloadCategoriesAsync();

        // Выбираем ту же категорию, что была в операции.
        // Сначала по Id, если не нашли — по имени (на случай удалённой категории).
        SelectedCategory =
            AvailableCategories.FirstOrDefault(c => c.Id == transaction.CategoryId)
            ?? AvailableCategories.FirstOrDefault(c => c.Name == transaction.CategoryName)
            ?? AvailableCategories.FirstOrDefault();

        AmountText = transaction.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        Date = transaction.Date;
        Note = transaction.Note ?? string.Empty;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));

        StatusMessage = $"Редактирование операции от {transaction.Date:dd.MM.yyyy}";
    }

    /// <summary>
    /// Выходит из режима редактирования, очищает форму.
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        _editingTransaction = null;
        _editingTransactionId = null;

        AmountText = string.Empty;
        Note = string.Empty;
        StatusMessage = string.Empty;

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    /// <summary>
    /// Применяет кастомный период: свапает границы, если они перепутаны,
    /// и перезагружает список.
    /// </summary>
    [RelayCommand]
    private async Task ApplyCustomRangeAsync()
    {
        // Если «от» позже «до» — меняем местами. Пользователь мог просто
        // перепутать поля, ошибка не критичная, не блокируем его диалогом.
        if (CustomFromDate > CustomToDate)
        {
            (CustomFromDate, CustomToDate) = (CustomToDate, CustomFromDate);
        }

        await ReloadTransactionsAsync();
    }

    /// <summary>
    /// Удаляет операцию после подтверждения. Вызывается из свайпа по карточке.
    /// </summary>
    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction? transaction)
    {
        if (transaction is null) return;
        if (IsBusy) return;

        // Формируем текст подтверждения с суммой и категорией
        var sign = transaction.Type == TransactionType.Income ? "+" : "−";
        var message = $"Удалить операцию?\n\n" +
                    $"{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}\n" +
                    $"{transaction.Date:dd.MM.yyyy}";

        var confirmed = await _dialogs.ConfirmAsync(
            title: "Удаление",
            message: message,
            accept: "Удалить",
            cancel: "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await _db.DeleteTransactionAsync(transaction.Id);

            // Если редактируется именно эта операция — выходим из режима редактирования
            if (_editingTransactionId == transaction.Id)
                CancelEdit();

            await ReloadTransactionsAsync();
            await ReloadBalanceAsync();

            StatusMessage = "Операция удалена";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка удаления: {ex.Message}";
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
            var (from, to) = GetDateRange();
            var transactions = await _db.GetTransactionsAsync(from, to, limit: 100, offset: 0);

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
    
    /// <summary>
    /// Возвращает диапазон дат для текущего фильтра.
    /// null означает «без ограничения с этой стороны».
    /// </summary>
    private (DateTime? From, DateTime? To) GetDateRange()
    {
        var today = DateTime.Today;

        return SelectedFilterIndex switch
        {
            0 => (today, today),
            1 => (today.AddDays(-1), today.AddDays(-1)),
            2 => (StartOfWeek(today), today),
            3 => (StartOfWeek(today).AddDays(-7), StartOfWeek(today).AddDays(-1)),
            4 => (new DateTime(today.Year, today.Month, 1), today),
            5 => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            6 => (new DateTime(today.Year, today.Month, 1).AddMonths(-2), today),
            7 => (new DateTime(today.Year, today.Month, 1).AddMonths(-11), today),
            8 => (null, null),
            9 => (CustomFromDate, CustomToDate),                         // ← новое
            _ => (new DateTime(today.Year, today.Month, 1), today),
        };
    }

    /// <summary>
    /// Начало недели (понедельник) для указанной даты.
    /// </summary>
    private static DateTime StartOfWeek(DateTime date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday).Date;
    }
}