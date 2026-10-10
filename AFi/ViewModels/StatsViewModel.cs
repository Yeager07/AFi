using System.Collections.ObjectModel;
using System.Globalization;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microcharts;
using SkiaSharp;

namespace AFi.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    private readonly DatabaseService _db;

    // ==================== Фильтр периода ====================

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

    public bool IsFilterVisible => IsDonutMode;

    // ==================== Тип диаграммы ====================

    [ObservableProperty] private int _chartModeIndex;

    public bool IsDonutMode => ChartModeIndex == 0;
    public bool IsBarMode => ChartModeIndex == 1;

    // ==================== Итоговые карточки ====================

    [ObservableProperty] private decimal _summaryIncome;
    [ObservableProperty] private decimal _summaryExpense;

    public decimal SummaryBalance => SummaryIncome - SummaryExpense;
    public bool IsBalanceNegative => SummaryBalance < 0;

    [ObservableProperty] private string _summaryPeriodLabel = "за выбранный период";

    // ==================== Круговая диаграмма ====================

    [ObservableProperty] private Chart? _expenseChart;
    [ObservableProperty] private string _totalText = "0 ₽";

    public ObservableCollection<LegendItem> LegendItems { get; } = new();

    // ==================== Столбчатые диаграммы ====================

    [ObservableProperty] private Chart? _incomeBarChart;
    [ObservableProperty] private Chart? _expenseBarChart;

    [ObservableProperty] private bool _hasNoData = true;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public StatsViewModel(DatabaseService db)
    {
        _db = db;
    }

    // ==================== Реакции ====================

    partial void OnSelectedFilterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomRangeSelected));
        if (value == 9) return;
        _ = ReloadAsync();
    }

    partial void OnChartModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsDonutMode));
        OnPropertyChanged(nameof(IsBarMode));
        OnPropertyChanged(nameof(IsFilterVisible));
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
    private async Task SelectDonutModeAsync()
    {
        if (ChartModeIndex == 0) return;
        ChartModeIndex = 0;
        await ReloadSummaryForCurrentModeAsync();
    }

    [RelayCommand]
    private async Task SelectBarModeAsync()
    {
        if (ChartModeIndex == 1) return;
        ChartModeIndex = 1;
        await ReloadSummaryForCurrentModeAsync();
    }

    /// <summary>
    /// Переход на страницу «История» с тем же периодом и категорией,
    /// что выбраны в Статистике.
    /// </summary>
    [RelayCommand]
    private async Task GoToHistoryAsync(LegendItem? item)
    {
        if (item is null) return;

        var fi = SelectedFilterIndex;
        var categoryParam = Uri.EscapeDataString(item.CategoryName);

        var url = $"history-push?filterIndex={fi}&category={categoryParam}";

        // Для «Свой период» нужно передать сами даты
        if (fi == 9)
        {
            url += $"&from={CustomFromDate:yyyy-MM-dd}&to={CustomToDate:yyyy-MM-dd}";
        }

        await Shell.Current.GoToAsync(url);
    }

    // ==================== Загрузка данных ====================

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _db.InitializeAsync();

            await ReloadSummaryForCurrentModeCoreAsync();

            var (from, to) = GetDateRange();
            await ReloadDonutAsync(from, to);

            await ReloadBarsAsync();

            OnPropertyChanged(nameof(IsBalanceNegative));
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

    private async Task ReloadSummaryForCurrentModeAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await ReloadSummaryForCurrentModeCoreAsync();
            OnPropertyChanged(nameof(IsBalanceNegative));
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

    private async Task ReloadSummaryForCurrentModeCoreAsync()
    {
        DateTime? from;
        DateTime? to;

        if (IsBarMode)
        {
            var today = DateTime.Today;
            var firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1);
            from = firstOfCurrentMonth.AddMonths(-5);
            to = today;
            SummaryPeriodLabel = "за последние 6 месяцев";
        }
        else
        {
            (from, to) = GetDateRange();
            SummaryPeriodLabel = SelectedFilterIndex == 8
                ? "за всё время"
                : "за выбранный период";
        }

        var (income, expense) = await _db.GetSummaryAsync(from, to);
        SummaryIncome = income;
        SummaryExpense = expense;
    }

    private async Task ReloadDonutAsync(DateTime? from, DateTime? to)
    {
        var breakdown = await _db.GetExpensesByCategoryAsync(from, to);

        decimal totalExpense = breakdown.Sum(x => x.Sum);
        TotalText = totalExpense.ToString("N0", CultureInfo.InvariantCulture) + " ₽";

        HasNoData = breakdown.Count == 0;
        LegendItems.Clear();

        if (HasNoData)
        {
            ExpenseChart = null;
            return;
        }

        ExpenseChart = BuildDonutChart(breakdown);

        for (int i = 0; i < breakdown.Count; i++)
        {
            var item = breakdown[i];
            var hex = ChartPalette[i % ChartPalette.Length];
            var percent = totalExpense > 0
                ? item.Sum / totalExpense * 100m
                : 0m;

            LegendItems.Add(new LegendItem
            {
                CategoryName = item.CategoryName,
                AmountText = item.Sum.ToString("N0", CultureInfo.InvariantCulture) + " ₽",
                PercentText = $"{percent:0.#}%",
                Color = Color.FromArgb(hex),
            });
        }
    }

    private async Task ReloadBarsAsync()
    {
        var today = DateTime.Today;
        var firstOfCurrentMonth = new DateTime(today.Year, today.Month, 1);
        var from = firstOfCurrentMonth.AddMonths(-5);
        var to = today;

        var monthly = await _db.GetMonthlyTotalsAsync(from, to);

        IncomeBarChart = BuildBarChart(monthly, income: true);
        ExpenseBarChart = BuildBarChart(monthly, income: false);
    }

    // ==================== Построение диаграмм ====================

    private static readonly string[] ChartPalette =
    {
        "#FF6B6B", "#4ECDC4", "#FFD93D", "#A8E6CF",
        "#B388FF", "#FF8A65", "#4DD0E1", "#BA68C8",
        "#81C784", "#FFB74D", "#7986CB", "#E57373",
    };

    private Chart BuildDonutChart(List<(string CategoryName, decimal Sum)> data)
    {
        var entries = data.Select((item, index) =>
        {
            var color = SKColor.Parse(ChartPalette[index % ChartPalette.Length]);
            return new ChartEntry((float)item.Sum)
            {
                Color = color,
            };
        }).ToArray();

        return new DonutChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            HoleRadius = 0.6f,
            LabelMode = LabelMode.None,
        };
    }

    private Chart BuildBarChart(
        List<(DateTime Month, decimal Income, decimal Expense)> monthly,
        bool income)
    {
        var barColor = income
            ? SKColor.Parse("#2E7D32")
            : SKColor.Parse("#D32F2F");

        var culture = new CultureInfo("ru-RU");

        var today = DateTime.Today;
        var months = Enumerable.Range(0, 6)
            .Select(i => new DateTime(today.Year, today.Month, 1).AddMonths(-5 + i))
            .ToList();

        var lookup = monthly.ToDictionary(m => m.Month, m => m);

        var entries = months.Select(month =>
        {
            var hasData = lookup.TryGetValue(month, out var m);
            var value = hasData
                ? (income ? m.Income : m.Expense)
                : 0m;

            return new ChartEntry((float)value)
            {
                Label = culture.DateTimeFormat
                    .GetAbbreviatedMonthName(month.Month)
                    .TrimEnd('.'),
                ValueLabel = value > 0
                    ? value.ToString("N0", CultureInfo.InvariantCulture)
                    : string.Empty,
                Color = barColor,
                TextColor = SKColors.Gray,
                ValueLabelColor = SKColors.Gray,
            };
        }).ToArray();

        return new BarChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 28,
            ValueLabelTextSize = 24,
        };
    }

    // ==================== Даты ====================

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