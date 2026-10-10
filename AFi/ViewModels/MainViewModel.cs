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

    /// <summary>
    /// Кнопка «Выбрать» видна только когда мы не в режиме выбора
    /// и есть хотя бы одна операция для выбора.
    /// </summary>
    public bool CanEnterSelectionMode => !IsSelectionMode && HasAnyTransactions;

    // ==================== Тип операции ====================

    public TransactionType CurrentType =>
        SelectedTypeIndex == 1 ? TransactionType.Income : TransactionType.Expense;

    public MainViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
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

    // ==================== Команды ====================

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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

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

        IsBusy = true;
        try
        {
            if (_editingTransaction is not null)
            {
                _editingTransaction.CategoryId = SelectedCategory.Id;
                _editingTransaction.CategoryName = SelectedCategory.Name;
                _editingTransaction.CategoryIcon = SelectedCategory.Icon;
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
                StatusMessage = "Операция сохранена";
            }

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

        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(SubmitButtonText));
    }

    [RelayCommand]
    private async Task DeleteTransactionAsync(Transaction? transaction)
    {
        if (transaction is null || IsBusy) return;

        var sign = transaction.Type == TransactionType.Income ? "+" : "−";
        var message = $"Удалить операцию?\n\n" +
                      $"{sign}{transaction.Amount:N0} ₽ · {transaction.CategoryName}\n" +
                      $"{transaction.Date:dd.MM.yyyy}";

        var confirmed = await _dialogs.ConfirmAsync(
            "Удаление", message, "Удалить", "Отмена");

        if (!confirmed) return;

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

    // ==================== Мультивыбор ====================

    /// <summary>
    /// Войти в режим мультивыбора (вызывается кнопкой «Выбрать»).
    /// </summary>
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

        System.Diagnostics.Debug.WriteLine($"ToggleSelection: id={transaction.Id}, before={transaction.IsSelected}");

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

        System.Diagnostics.Debug.WriteLine($"ToggleSelection: after={transaction.IsSelected}, count={_selectedIds.Count}");

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

    /// <summary>
    /// Тап по карточке операции: в обычном режиме — редактирование,
    /// в режиме мультивыбора — переключение выбора.
    /// </summary>
    [RelayCommand]
    private async Task HandleCardTapAsync(Transaction? transaction)
    {
        if (transaction is null) return;

        if (IsSelectionMode)
        {
            ToggleSelection(transaction);
            return;
        }

        await StartEditAsync(transaction);
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