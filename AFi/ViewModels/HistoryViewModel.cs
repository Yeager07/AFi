using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

/// <summary>
/// ViewModel страницы «История»: фильтры по периоду и категории,
/// итоговые карточки, постраничный список операций.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private const int PageSize = 100;

    private readonly DatabaseService _db;
    private readonly IDialogService _dialogs;

    private int _loadedCount;

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

    /// <summary>
    /// Элементы для Picker выбора категории.
    /// Первый — «Все категории» (Id = null), далее реальные категории.
    /// </summary>
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

    public HistoryViewModel(DatabaseService db, IDialogService dialogs)
    {
        _db = db;
        _dialogs = dialogs;
    }

    // ==================== Реакции ====================

    partial void OnSelectedFilterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomRangeSelected));
        if (value == 9) return; // ждём кнопку «Применить»
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
            await LoadCoreAsync();                        // ← вызываем обход IsBusy
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

    /// <summary>
    /// Загрузка без проверки IsBusy — вызывается из методов,
    /// которые уже держат флаг.
    /// </summary>
    private async Task LoadCoreAsync()
    {
        await _db.InitializeAsync();

        var (from, to) = GetDateRange();
        var categoryId = GetSelectedCategoryId();

        // Список — первая страница
        var list = await _db.GetTransactionsAsync(from, to, categoryId,
            limit: PageSize, offset: 0);

        Transactions.Clear();
        foreach (var t in list)
            Transactions.Add(t);

        _loadedCount = list.Count;

        // Итоги за период
        var (income, expense) = await _db.GetSummaryAsync(from, to, categoryId);
        SummaryIncome = income;
        SummaryExpense = expense;

        // Есть ли ещё
        var total = await _db.GetTransactionsCountAsync(from, to, categoryId);
        HasMoreItems = _loadedCount < total;

        UpdateCountInStatus(total);
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(IsBalanceNegative));
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
}

/// <summary>
/// Элемент списка выбора категории. Id = null означает «без фильтра».
/// </summary>
public class CategoryFilterItem
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}