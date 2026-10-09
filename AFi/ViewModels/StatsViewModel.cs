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

    // ==================== Итоговые карточки ====================

    [ObservableProperty] private decimal _summaryIncome;
    [ObservableProperty] private decimal _summaryExpense;

    public decimal SummaryBalance => SummaryIncome - SummaryExpense;
    public bool IsBalanceNegative => SummaryBalance < 0;

    // ==================== Диаграмма ====================

    [ObservableProperty] private Chart? _expenseChart;

    [ObservableProperty] private string _totalText = "0 ₽";

    [ObservableProperty] private bool _hasNoData;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// Легенда под диаграммой: категория, сумма, процент, цвет.
    /// </summary>
    public ObservableCollection<LegendItem> LegendItems { get; } = new();

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

    // ==================== Загрузка данных ====================

    private async Task ReloadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            await _db.InitializeAsync();

            var (from, to) = GetDateRange();

            // Итоговые карточки
            var (income, expense) = await _db.GetSummaryAsync(from, to);
            SummaryIncome = income;
            SummaryExpense = expense;

            // Разбивка по категориям для диаграммы и легенды
            var breakdown = await _db.GetExpensesByCategoryAsync(from, to);

            decimal totalExpense = breakdown.Sum(x => x.Sum);
            TotalText = totalExpense.ToString("N0", CultureInfo.InvariantCulture) + " ₽";

            HasNoData = breakdown.Count == 0;

            LegendItems.Clear();

            if (HasNoData)
            {
                ExpenseChart = null;
                StatusMessage = "Нет расходов за выбранный период";
            }
            else
            {
                ExpenseChart = BuildDonutChart(breakdown);

                // Легенда: категория, сумма, процент, цвет
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

                StatusMessage = $"Категорий: {breakdown.Count}";
            }

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

    // ==================== Построение диаграммы ====================

    private static readonly string[] ChartPalette =
    {
        "#FF6B6B", "#4ECDC4", "#FFD93D", "#A8E6CF",
        "#B388FF", "#FF8A65", "#4DD0E1", "#BA68C8",
        "#81C784", "#FFB74D", "#7986CB", "#E57373",
    };

    private Chart BuildDonutChart(List<(string CategoryName, decimal Sum)> data)
    {
        // Label и ValueLabel не задаём — они не нужны, диаграмма теперь без подписей.
        // Цвета те же, что и в легенде.
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
            LabelMode = LabelMode.None,   // ← отключаем подписи вокруг диаграммы
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