using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int PageSize = 10;
    private const int PeriodDays = 30;

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    private int? _editingTransactionId;
    private Transaction? _editingTransaction;
    private bool _suppressTypeChangeReload;

    private int _loadedCount = PageSize;
    private int _totalInPeriod;

    private readonly HashSet<int> _selectedIds = new();

    // ==================== Состояние формы ====================

    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private int _selectedTypeIndex = 0;
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private decimal _currentBalance;
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;

    public bool IsEditing => _editingTransactionId.HasValue;
    public string FormTitle => IsEditing ? "Редактирование" : "Новая операция";
    public string SubmitButtonText => IsEditing ? "Обновить" : "Сохранить";
    public bool IsBalanceNegative => CurrentBalance < 0;

    // ==================== Позиции чека ====================

    /// <summary>
    /// Текущие позиции формы. При создании операции — пустые,
    /// при редактировании — загружаются из БД.
    /// </summary>
    public ObservableCollection<TransactionItem> CurrentItems { get; } = new();

    [ObservableProperty] private bool _hasItems;

    /// <summary>
    /// Итоговая строка «Сумма по позициям: X ₽».
    /// </summary>
    [ObservableProperty] private string _itemsTotalText = string.Empty;

    /// <summary>
    /// Показать ли предупреждение о расхождении суммы операции и суммы позиций.
    /// </summary>
    [ObservableProperty] private bool _hasAmountMismatch;
    [ObservableProperty] private string _amountMismatchText = string.Empty;

    // ==================== Списки ====================

    public ObservableCollection<string> TypeOptions { get; } = new()
    {
        "Расход",
        "Доход"
    };

    public ObservableCollection<Category> AvailableCategories { get; } = new();

    public ObservableCollection<TransactionGroup> TransactionGroups { get; } = new();

    // ==================== Пагинация ====================

    [ObservableProperty] private bool _hasMoreInPeriod;
    [ObservableProperty] private string _showMoreButtonText = "Показать ещё";

    public bool HasAnyTransactions => _totalInPeriod > 0;

    // ==================== Мультивыбор ====================

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;

    public bool HasSelection => SelectedCount > 0;

    public bool CanEnterSelectionMode => !IsSelectionMode && HasAnyTransactions;

    // ==================== Тип операции ====================

    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public MainViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;

        // Пересчитывать итог при каждом изменении коллекции позиций
        CurrentItems.CollectionChanged += (_, _) => RefreshItemsTotal();
    }

    // ==================== Реакции ====================

    partial void OnSelectedTypeIndexChanged(int value)
    {
        if (_suppressTypeChangeReload) return;
        _ = ReloadCategoriesAsync();
    }

    partial void OnCurrentBalanceChanged(decimal value) =>
        OnPropertyChanged(nameof(IsBalanceNegative));

    partial void OnSelectedCountChanged(int value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnIsSelectionModeChanged(bool value) =>
        OnPropertyChanged(nameof(CanEnterSelectionMode));

    partial void OnAmountTextChanged(string value) =>
        RefreshAmountMismatch();

    // ==================== Загрузка и навигация ====================

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

            _loadedCount = PageSize;
            await LoadTransactionsPageAsync();

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

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMoreInPeriod) return;

        _loadedCount += PageSize;
        await LoadTransactionsPageAsync();
    }

    [RelayCommand]
    private async Task GoToHistoryAsync()
    {
        await Shell.Current.GoToAsync("history-push");
    }

    // ==================== Позиции чека ====================

    [RelayCommand]
    private void AddItem()
    {
        CurrentItems.Add(new TransactionItem
        {
            Name = string.Empty,
            Quantity = 1m,
            UnitPrice = 0m,
        });
    }

    [RelayCommand]
    private void RemoveItem(TransactionItem? item)
    {
        if (item is null) return;
        CurrentItems.Remove(item);
    }

    /// <summary>
    /// Пересчитывает итог по позициям и обновляет состояние UI.
    /// Вызывается автоматически при изменении коллекции, а также
    /// из code-behind при вводе в Entry позиции.
    /// </summary>
    public void RefreshItemsTotal()
    {
        HasItems = CurrentItems.Count > 0;

        if (!HasItems)
        {
            ItemsTotalText = string.Empty;
            RefreshAmountMismatch();
            return;
        }

        var total = CurrentItems.Sum(i => i.Total);
        ItemsTotalText = $"Сумма по позициям: {total:N0} ₽";

        RefreshAmountMismatch();
    }

    /// <summary>
    /// Показывает предупреждение, если сумма операции не совпадает с суммой позиций.
    /// </summary>
    private void RefreshAmountMismatch()
    {
        if (!HasItems)
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        // Парсим сумму из поля «Сумма»
        var normalized = (AmountText ?? string.Empty).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number,
                              CultureInfo.InvariantCulture, out var amount))
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        var itemsTotal = CurrentItems.Sum(i => i.Total);
        var diff = amount - itemsTotal;

        if (Math.Abs(diff) < 0.01m)
        {
            HasAmountMismatch = false;
            AmountMismatchText = string.Empty;
            return;
        }

        HasAmountMismatch = true;
        AmountMismatchText = diff > 0
            ? $"Позиции: {itemsTotal:N0} ₽. Сумма операции больше на {diff:N0} ₽."
            : $"Позиции: {itemsTotal:N0} ₽. Сумма операции меньше на {Math.Abs(diff):N0} ₽.";
    }

    // ==================== Сохранение ====================

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        // Если есть позиции — сумма всегда = сумме позиций,
        // и при создании, и при редактировании.
        // Позиции считаются "источником истины".
        decimal? itemsTotal = null;
        if (HasItems)
        {
            itemsTotal = CurrentItems.Sum(i => i.Total);
            if (itemsTotal > 0)
                AmountText = itemsTotal.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        decimal amount;
        if (_editingTransaction is not null)
        {
            amount = _editingTransaction.Amount;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(AmountText))
            {
                StatusMessage = "Введите сумму или добавьте позиции";
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

        // Валидация позиций
        foreach (var item in CurrentItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                StatusMessage = "У всех позиций должно быть название";
                return;
            }
            if (item.Quantity <= 0)
            {
                StatusMessage = "У всех позиций должно быть количество больше нуля";
                return;
            }
        }

        IsBusy = true;
        try
        {
            int transactionId;

            if (_editingTransaction is not null)
            {
                _editingTransaction.CategoryId = SelectedCategory.Id;
                _editingTransaction.CategoryName = SelectedCategory.Name;
                _editingTransaction.CategoryIcon = SelectedCategory.Icon;
                _editingTransaction.Date = Date;
                _editingTransaction.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

                // Если есть позиции — обновляем сумму из них,
                // иначе сумма остаётся неизменной (immutable)
                if (itemsTotal.HasValue && itemsTotal.Value > 0)
                    _editingTransaction.Amount = itemsTotal.Value;

                await _db.UpdateTransactionAsync(_editingTransaction);

                transactionId = _editingTransaction.Id;

                _editingTransactionId = null;
                _editingTransaction = null;
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(SubmitButtonText));

                StatusMessage = "Операция обновлена";
            }
            else
            {
                var transaction = new Transaction
                {
                    Amount = amount,
                    Type = CurrentType,
                    CategoryId = SelectedCategory.Id,
                    CategoryName = SelectedCategory.Name,
                    CategoryIcon = SelectedCategory.Icon,
                    Date = Date,
                    Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    CreatedAt = DateTime.Now
                };

                await _db.AddTransactionAsync(transaction);
                transactionId = transaction.Id;

                StatusMessage = "Операция сохранена";
            }

            // Сохраняем позиции чека
            await _db.ReplaceItemsForTransactionAsync(transactionId, CurrentItems.ToList());

            ResetForm();

            _loadedCount = PageSize;
            await LoadTransactionsPageAsync();
            await ReloadBalanceAsync();
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

    // ==================== Редактирование ====================

    [RelayCommand]
    private async Task StartEditAsync(Transaction? transaction)
    {
        if (transaction is null) return;

        _editingTransaction = transaction;
        _editingTransactionId = transaction.Id;

        _suppressTypeChangeReload = true;
        SelectedTypeIndex = transaction.Type == TransactionType.Income ? 1 : 0;
        _suppressTypeChangeReload = false;

        await ReloadCategoriesAsync();

        SelectedCategory =
            AvailableCategories.FirstOrDefault(c => c.Id == transaction.CategoryId)
            ?? AvailableCategories.FirstOrDefault(c => c.Name == transaction.CategoryName)
            ?? AvailableCategories.FirstOrDefault();

        AmountText = transaction.Amount.ToString("0.##", CultureInfo.InvariantCulture);
        Date = transaction.Date;
        Note = transaction.Note ?? string.Empty;

        // Загружаем позиции чека
        CurrentItems.Clear();
        var items = await _db.GetItemsForTransactionAsync(transaction.Id);
        foreach (var item in items)
            CurrentItems.Add(item);

        RefreshItemsTotal();

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));

        StatusMessage = $"Редактирование операции от {transaction.Date:dd.MM.yyyy}";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingTransactionId = null;
        _editingTransaction = null;

        AmountText = string.Empty;
        Note = string.Empty;
        StatusMessage = string.Empty;

        CurrentItems.Clear();

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    // ==================== Удаление ====================

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction? transaction)
    {
        await DeleteTransactionCoreAsync(transaction, skipConfirmation: false);
    }

    private async Task DeleteTransactionCoreAsync(Transaction? transaction, bool skipConfirmation)
    {
        if (transaction is null || IsBusy) return;

        if (!skipConfirmation)
        {
            var sign = transaction.Type == TransactionType.Income ? "+" : "−";
            var message = $"Удалить операцию?\n\n" +
                          $"{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}\n" +
                          $"{transaction.Date:dd.MM.yyyy}";

            var confirmed = await _dialogs.ConfirmAsync(
                "Удаление", message, "Удалить", "Отмена");

            if (!confirmed) return;
        }

        IsBusy = true;
        try
        {
            await _db.DeleteTransactionAsync(transaction.Id);

            if (_editingTransactionId == transaction.Id)
                CancelEdit();

            _loadedCount = PageSize;
            await LoadTransactionsPageAsync();
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

    // ==================== Тап по карточке ====================

    [RelayCommand]
    private async Task HandleCardTapAsync(Transaction? transaction)
    {
        if (transaction is null) return;

        if (IsSelectionMode)
        {
            ToggleSelection(transaction);
            return;
        }

        var sign = transaction.Type == TransactionType.Income ? "+" : "−";
        var title = $"{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}";

        var choice = await _dialogs.ShowActionSheetAsync(
            title,
            "Отмена",
            "Редактировать",
            "Удалить");

        if (choice == "Редактировать")
        {
            await StartEditAsync(transaction);
        }
        else if (choice == "Удалить")
        {
            await DeleteTransactionCoreAsync(transaction, skipConfirmation: true);
        }
    }

    /// <summary>
    /// Развернуть/свернуть карточку операции для показа позиций чека.
    /// В режиме мультивыбора не работает — там тап переключает выбор.
    /// </summary>
    [RelayCommand]
    private void ToggleExpand(Transaction? transaction)
    {
        if (transaction is null) return;
        if (IsSelectionMode) return;

        transaction.IsExpanded = !transaction.IsExpanded;
    }

    // ==================== Мультивыбор ====================

    [RelayCommand]
    private void EnterSelectionMode()
    {
        if (!HasAnyTransactions) return;
        IsSelectionMode = true;
    }

    [RelayCommand]
    private void ToggleSelection(Transaction? transaction)
    {
        if (transaction is null || !IsSelectionMode) return;

        if (_selectedIds.Contains(transaction.Id))
        {
            _selectedIds.Remove(transaction.Id);
            transaction.IsSelected = false;
        }
        else
        {
            _selectedIds.Add(transaction.Id);
            transaction.IsSelected = true;
        }

        SelectedCount = _selectedIds.Count;
    }

    [RelayCommand]
    private void CancelSelection()
    {
        foreach (var group in TransactionGroups)
        {
            foreach (var t in group)
                t.IsSelected = false;
        }

        _selectedIds.Clear();
        SelectedCount = 0;
        IsSelectionMode = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || IsBusy) return;

        var count = _selectedIds.Count;
        var word = Plural(count, "операцию", "операции", "операций");

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление",
            $"Удалить {count} {word}?\n\nЭто действие нельзя отменить.",
            "Удалить",
            "Отмена");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            foreach (var id in _selectedIds.ToList())
                await _db.DeleteTransactionAsync(id);

            _selectedIds.Clear();
            SelectedCount = 0;
            IsSelectionMode = false;

            _loadedCount = PageSize;
            await LoadTransactionsPageAsync();
            await ReloadBalanceAsync();

            StatusMessage = $"Удалено {count} {word}";
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

    // ==================== Вспомогательные ====================

    private async Task ReloadCategoriesAsync()
    {
        try
        {
            var previousName = SelectedCategory?.Name;

            var categories = await _db.GetCategoriesAsync(CurrentType);

            AvailableCategories.Clear();
            foreach (var c in categories)
                AvailableCategories.Add(c);

            SelectedCategory =
                AvailableCategories.FirstOrDefault(c => c.Name == previousName)
                ?? AvailableCategories.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки категорий: {ex.Message}";
        }
    }

    private async Task LoadTransactionsPageAsync()
    {
        try
        {
            var to = DateTime.Today;
            var from = to.AddDays(-(PeriodDays - 1));

            var all = await _db.GetTransactionsAsync(from, to, null,
                limit: 10000, offset: 0);

            _totalInPeriod = all.Count;

            var toShow = all.Take(_loadedCount).ToList();

            // Загружаем позиции одним запросом для всех показанных операций
            var ids = toShow.Select(t => t.Id).ToList();
            var itemsByTransaction = await _db.GetItemsForTransactionsAsync(ids);

            foreach (var t in toShow)
            {
                if (itemsByTransaction.TryGetValue(t.Id, out var items))
                {
                    var collection = new System.Collections.ObjectModel.ObservableCollection<TransactionItem>();
                    foreach (var item in items)
                        collection.Add(item);
                    t.Items = collection;
                }
            }

            TransactionGroups.Clear();
            foreach (var g in toShow
                        .GroupBy(t => t.Date.Date)
                        .OrderByDescending(g => g.Key))
            {
                TransactionGroups.Add(new TransactionGroup(g.Key, g.ToList()));
            }

            HasMoreInPeriod = _loadedCount < _totalInPeriod;
            ShowMoreButtonText = HasMoreInPeriod ? "Показать ещё" : "Вся история";

            OnPropertyChanged(nameof(HasAnyTransactions));
            OnPropertyChanged(nameof(CanEnterSelectionMode));

            _selectedIds.Clear();
            SelectedCount = 0;
            IsSelectionMode = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки операций: {ex.Message}";
        }
    }

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

    private void ResetForm()
    {
        AmountText = string.Empty;
        Note = string.Empty;
        CurrentItems.Clear();
    }

    private static string Plural(int count, string one, string few, string many)
    {
        int mod100 = count % 100;
        int mod10 = count % 10;
        if (mod100 >= 11 && mod100 <= 14) return many;
        if (mod10 == 1) return one;
        if (mod10 >= 2 && mod10 <= 4) return few;
        return many;
    }
}