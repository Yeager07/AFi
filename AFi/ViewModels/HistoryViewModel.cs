using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class HistoryViewModel : ObservableObject, IQueryAttributable
{
    private const int PageSize = 100;

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;
    private readonly IReceiptService _receipts;

    private int _loadedCount;
    private readonly HashSet<int> _selectedIds = new();
    private bool _suppressFilterReload;
    private bool _filtersLoaded;

    // ==================== Фильтры ====================

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

    [ObservableProperty] private int _selectedFilterIndex = 4;

    [ObservableProperty] private DateTime _customFromDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _customToDate = DateTime.Today;
    [ObservableProperty] private DateTime _maxDate = DateTime.Today;

    public bool IsCustomRangeSelected => SelectedFilterIndex == 9;

    public ObservableCollection<CategoryFilterItem> CategoryFilterItems { get; } = new();

    [ObservableProperty] private int _selectedCategoryIndex = 0;

    // ==================== Данные ====================

    public ObservableCollection<Transaction> Transactions { get; } = new();

    [ObservableProperty] private decimal _summaryIncome;
    [ObservableProperty] private decimal _summaryExpense;

    public decimal SummaryBalance => SummaryIncome - SummaryExpense;
    public bool IsBalanceNegative => SummaryBalance < 0;

    [ObservableProperty] private bool _hasMoreItems;
    [ObservableProperty] private bool _isLoadingMore;

    public bool HasTransactions => Transactions.Count > 0;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // ==================== Мультивыбор ====================

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;

    public bool HasSelection => SelectedCount > 0;
    public bool CanEnterSelectionMode => !IsSelectionMode && HasTransactions;

    private string? _pendingCategoryName;

    public HistoryViewModel(DatabaseService db, IDialogService dialogs, IReceiptService receipts)
    {
        _db = db;
        _dialogs = dialogs;
        _receipts = receipts;
    }

    // ==================== Применение query-параметров ====================

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _suppressFilterReload = true;
        try
        {
            if (query.TryGetValue("filterIndex", out var fiObj)
                && int.TryParse(fiObj?.ToString(), out var fi)
                && fi >= 0 && fi < FilterOptions.Count)
            {
                SelectedFilterIndex = fi;
            }

            if (query.TryGetValue("from", out var fromObj)
                && DateTime.TryParse(fromObj?.ToString(), out var fromDate))
            {
                CustomFromDate = fromDate;
            }

            if (query.TryGetValue("to", out var toObj)
                && DateTime.TryParse(toObj?.ToString(), out var toDate))
            {
                CustomToDate = toDate;
            }

            if (query.TryGetValue("category", out var catObj)
                && catObj is string catName
                && !string.IsNullOrWhiteSpace(catName))
            {
                _pendingCategoryName = catName;
            }
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private void ApplyPendingCategoryFilter()
    {
        if (string.IsNullOrWhiteSpace(_pendingCategoryName)) return;

        var catName = _pendingCategoryName;
        _pendingCategoryName = null;

        for (int i = 0; i < CategoryFilterItems.Count; i++)
        {
            if (CategoryFilterItems[i].FilterName == catName)
            {
                _suppressFilterReload = true;
                SelectedCategoryIndex = i;
                _suppressFilterReload = false;
                return;
            }
        }

        var virtualItem = new CategoryFilterItem
        {
            Id = null,
            Name = $"❓ {catName} (удалена)",
            FilterName = catName,
        };
        CategoryFilterItems.Add(virtualItem);
        _suppressFilterReload = true;
        SelectedCategoryIndex = CategoryFilterItems.Count - 1;
        _suppressFilterReload = false;
    }

    // ==================== Реакции ====================

    partial void OnSelectedFilterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomRangeSelected));
        if (_suppressFilterReload) return;
        if (value == 9) return;
        _ = ReloadAsync();
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        if (_suppressFilterReload) return;
        _ = ReloadAsync();
    }

    partial void OnSummaryIncomeChanged(decimal value) =>
        OnPropertyChanged(nameof(SummaryBalance));
    partial void OnSummaryExpenseChanged(decimal value) =>
        OnPropertyChanged(nameof(SummaryBalance));

    partial void OnSelectedCountChanged(int value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnIsSelectionModeChanged(bool value) =>
        OnPropertyChanged(nameof(CanEnterSelectionMode));

    // ==================== Команды ====================

    [RelayCommand]
    private async Task LoadAsync()
    {
        MaxDate = DateTime.Today;

        // Фильтры пересобираем только при первом заходе.
        // При повторных OnAppearing (возврат с модалки Picker-а,
        // переключение страниц) сохраняем уже выбранные значения.
        if (!_filtersLoaded)
        {
            await ReloadFiltersAsync();
            ApplyPendingCategoryFilter();
            _filtersLoaded = true;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ApplyCustomRangeAsync()
    {
        if (CustomFromDate > CustomToDate)
            (CustomFromDate, CustomToDate) = (CustomToDate, CustomFromDate);

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !HasMoreItems) return;
        IsLoadingMore = true;

        try
        {
            var (from, to) = GetDateRange();
            var categoryName = GetSelectedCategoryName();

            var next = await _db.GetTransactionsAsync(from, to,
                limit: PageSize, offset: _loadedCount,
                categoryName: categoryName);

            var newIds = next.Select(t => t.Id).ToList();
            var newItemsByTransaction = await _db.GetItemsForTransactionsAsync(newIds);

            foreach (var t in next)
            {
                if (newItemsByTransaction.TryGetValue(t.Id, out var items))
                {
                    var collection = new ObservableCollection<TransactionItem>();
                    foreach (var item in items)
                        collection.Add(item);
                    t.Items = collection;
                }

                if (!string.IsNullOrEmpty(t.ReceiptImagePath))
                {
                    var path = _receipts.GetFullPath(t.ReceiptImagePath);
                    if (path is not null)
                        t.ReceiptImageSource = ImageSource.FromFile(path);
                }
            }

            foreach (var t in next)
                Transactions.Add(t);

            _loadedCount += next.Count;

            var total = await _db.GetTransactionsCountAsync(from, to,
                categoryName: categoryName);
            HasMoreItems = _loadedCount < total;
            UpdateCountInStatus(total);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
        finally
        {
            IsLoadingMore = false;
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
            "Удалить");

        if (choice == "Удалить")
        {
            await DeleteTransactionCoreAsync(transaction, skipConfirmation: true);
        }
    }

    [RelayCommand]
    private void ToggleExpand(Transaction? transaction)
    {
        if (transaction is null) return;
        if (IsSelectionMode) return;

        transaction.IsExpanded = !transaction.IsExpanded;
    }

    /// <summary>
    /// Открыть фото чека на весь экран.
    /// </summary>
    [RelayCommand]
    private async Task ViewReceiptAsync(Transaction? transaction)
    {
        if (transaction?.ReceiptImageSource is null) return;

        await _dialogs.ShowReceiptAsync(transaction.ReceiptImageSource);
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
            if (!string.IsNullOrEmpty(transaction.ReceiptImagePath))
                await _receipts.DeleteAsync(transaction.ReceiptImagePath);

            await _db.DeleteTransactionAsync(transaction.Id);
            await LoadCoreAsync();
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

    [RelayCommand]
    private void EnterSelectionMode()
    {
        if (!HasTransactions) return;
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
        foreach (var t in Transactions)
            t.IsSelected = false;

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
            {
                var transaction = Transactions.FirstOrDefault(t => t.Id == id);
                if (transaction is not null
                    && !string.IsNullOrEmpty(transaction.ReceiptImagePath))
                {
                    await _receipts.DeleteAsync(transaction.ReceiptImagePath);
                }

                await _db.DeleteTransactionAsync(id);
            }

            _selectedIds.Clear();
            SelectedCount = 0;
            IsSelectionMode = false;

            await LoadCoreAsync();

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

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await LoadCoreAsync();
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

    private async Task LoadCoreAsync()
    {
        await _db.InitializeAsync();

        var (from, to) = GetDateRange();
        var categoryName = GetSelectedCategoryName();

        var list = await _db.GetTransactionsAsync(from, to,
            limit: PageSize, offset: 0,
            categoryName: categoryName);

        var ids = list.Select(t => t.Id).ToList();
        var itemsByTransaction = await _db.GetItemsForTransactionsAsync(ids);

        foreach (var t in list)
        {
            if (itemsByTransaction.TryGetValue(t.Id, out var items))
            {
                var collection = new ObservableCollection<TransactionItem>();
                foreach (var item in items)
                    collection.Add(item);
                t.Items = collection;
            }

            if (!string.IsNullOrEmpty(t.ReceiptImagePath))
            {
                var path = _receipts.GetFullPath(t.ReceiptImagePath);
                if (path is not null)
                    t.ReceiptImageSource = ImageSource.FromFile(path);
            }
        }

        Transactions.Clear();
        foreach (var t in list)
            Transactions.Add(t);

        _loadedCount = list.Count;

        var (income, expense) = await _db.GetSummaryAsync(from, to,
            categoryName: categoryName);
        SummaryIncome = income;
        SummaryExpense = expense;

        var total = await _db.GetTransactionsCountAsync(from, to,
            categoryName: categoryName);
        HasMoreItems = _loadedCount < total;

        UpdateCountInStatus(total);
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsBalanceNegative));
        OnPropertyChanged(nameof(CanEnterSelectionMode));

        _selectedIds.Clear();
        SelectedCount = 0;
        IsSelectionMode = false;
    }

    private void UpdateCountInStatus(int total)
    {
        StatusMessage = total == 0
            ? "Нет операций за выбранный период"
            : $"Показано {_loadedCount} из {total}";
    }

    private async Task ReloadFiltersAsync()
    {
        try
        {
            await _db.InitializeAsync();

            var categories = await _db.GetAllCategoriesAsync();

            var previousFilterName = GetSelectedCategoryName();

            CategoryFilterItems.Clear();
            CategoryFilterItems.Add(new CategoryFilterItem
            {
                Id = null,
                Name = "Все категории",
                FilterName = "",
            });

            foreach (var c in categories)
            {
                var icon = string.IsNullOrEmpty(c.Icon) ? "👤" : c.Icon;
                var typeRu = c.Type == TransactionType.Income ? "доход" : "расход";
                CategoryFilterItems.Add(new CategoryFilterItem
                {
                    Id = c.Id,
                    Name = $"{icon} {c.Name} ({typeRu})",
                    FilterName = c.Name,
                });
            }

            if (!string.IsNullOrEmpty(previousFilterName))
            {
                for (int i = 0; i < CategoryFilterItems.Count; i++)
                {
                    if (CategoryFilterItems[i].FilterName == previousFilterName)
                    {
                        _suppressFilterReload = true;
                        SelectedCategoryIndex = i;
                        _suppressFilterReload = false;
                        return;
                    }
                }
            }

            _suppressFilterReload = true;
            SelectedCategoryIndex = 0;
            _suppressFilterReload = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки фильтров: {ex.Message}";
        }
    }

    private string? GetSelectedCategoryName()
    {
        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= CategoryFilterItems.Count)
            return null;

        var item = CategoryFilterItems[SelectedCategoryIndex];
        return string.IsNullOrEmpty(item.FilterName) ? null : item.FilterName;
    }

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
            9 => (CustomFromDate, CustomToDate),
            _ => (new DateTime(today.Year, today.Month, 1), today),
        };
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday).Date;
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

public class CategoryFilterItem
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilterName { get; set; } = string.Empty;

    public override string ToString() => Name;

    /// <summary>
    /// Сравнение по FilterName — чтобы IndexOf находил категорию
    /// даже после пересоздания коллекции в ReloadFiltersAsync
    /// (при возврате на страницу или с модалки Picker-а).
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not CategoryFilterItem other) return false;
        return FilterName == other.FilterName;
    }

    public override int GetHashCode() => FilterName?.GetHashCode() ?? 0;
}