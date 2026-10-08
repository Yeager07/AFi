using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private const int PageSize = 100;

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    private int _loadedCount;
    private readonly HashSet<int> _selectedIds = new();

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

    /// <summary>
    /// Кнопка «Выбрать» видна, когда мы не в режиме выбора и есть что выбирать.
    /// </summary>
    public bool CanEnterSelectionMode => !IsSelectionMode && HasTransactions;

    public HistoryViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
    }

    // ==================== Реакции ====================

    partial void OnSelectedFilterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomRangeSelected));
        if (value == 9) return;
        _ = ReloadAsync();
    }

    partial void OnSelectedCategoryIndexChanged(int value)
    {
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
        await ReloadFiltersAsync();
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
            var categoryId = GetSelectedCategoryId();

            var next = await _db.GetTransactionsAsync(from, to, categoryId,
                limit: PageSize, offset: _loadedCount);

            foreach (var t in next)
                Transactions.Add(t);

            _loadedCount += next.Count;

            var total = await _db.GetTransactionsCountAsync(from, to, categoryId);
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
                await _db.DeleteTransactionAsync(id);

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

    /// <summary>
    /// Тап по карточке: в обычном режиме — редактирование (пока не реализовано
    /// в Истории), в режиме мультивыбора — toggle выбора.
    /// </summary>
    [RelayCommand]
    private void HandleCardTap(Transaction? transaction)
    {
        if (transaction is null) return;

        if (IsSelectionMode)
        {
            ToggleSelection(transaction);
        }
        // Вне режима выбора пока ничего — редактирование в Истории появится позже
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
        var categoryId = GetSelectedCategoryId();

        var list = await _db.GetTransactionsAsync(from, to, categoryId,
            limit: PageSize, offset: 0);

        Transactions.Clear();
        foreach (var t in list)
            Transactions.Add(t);

        _loadedCount = list.Count;

        var (income, expense) = await _db.GetSummaryAsync(from, to, categoryId);
        SummaryIncome = income;
        SummaryExpense = expense;

        var total = await _db.GetTransactionsCountAsync(from, to, categoryId);
        HasMoreItems = _loadedCount < total;

        UpdateCountInStatus(total);
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsBalanceNegative));
        OnPropertyChanged(nameof(CanEnterSelectionMode));

        // Сбрасываем режим выбора при любой перезагрузке
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

            CategoryFilterItems.Clear();
            CategoryFilterItems.Add(new CategoryFilterItem { Id = null, Name = "Все категории" });
            foreach (var c in categories)
                CategoryFilterItems.Add(new CategoryFilterItem
                {
                    Id = c.Id,
                    Name = $"{c.Name} ({(c.Type == TransactionType.Income ? "доход" : "расход")})",
                });

            SelectedCategoryIndex = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки фильтров: {ex.Message}";
        }
    }

    private int? GetSelectedCategoryId()
    {
        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= CategoryFilterItems.Count)
            return null;
        return CategoryFilterItems[SelectedCategoryIndex].Id;
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
    public override string ToString() => Name;
}