using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microcharts;
using SkiaSharp;

namespace AFi.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    /// <summary>
    /// Тестовая круговая диаграмма — для проверки, что Microcharts работает.
    /// На шаге 3.2 заменим на реальные данные из БД.
    /// </summary>
    [ObservableProperty]
    private Chart? _testChart;

    /// <summary>
    /// Сумма в центре диаграммы. Форматируется как "40 000 ₽".
    /// </summary>
    [ObservableProperty]
    private string _totalText = string.Empty;

    public StatsViewModel()
    {
        // Цвет текста в легенде зависит от текущей темы системы.
        // На шаге 3.4 будем брать из ресурсов темы, пока — из системной.
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark
            ? SKColor.Parse("#FFFFFF")
            : SKColor.Parse("#000000");

        var entries = new[]
        {
            new ChartEntry(15000)
            {
                Label = "Аренда",
                ValueLabel = "15 000",
                Color = SKColor.Parse("#FF6B6B"),
                TextColor = textColor,
                ValueLabelColor = textColor,
            },
            new ChartEntry(12000)
            {
                Label = "Закупки",
                ValueLabel = "12 000",
                Color = SKColor.Parse("#4ECDC4"),
                TextColor = textColor,
                ValueLabelColor = textColor,
            },
            new ChartEntry(8000)
            {
                Label = "Транспорт",
                ValueLabel = "8 000",
                Color = SKColor.Parse("#FFD93D"),
                TextColor = textColor,
                ValueLabelColor = textColor,
            },
            new ChartEntry(5000)
            {
                Label = "Прочее",
                ValueLabel = "5 000",
                Color = SKColor.Parse("#A8E6CF"),
                TextColor = textColor,
                ValueLabelColor = textColor,
            },
        };

        TestChart = new DonutChart
        {
            Entries = entries,
            BackgroundColor = SKColors.Transparent,
            LabelTextSize = 32,
            HoleRadius = 0.6f,
        };

        decimal total = entries.Sum(e => (decimal)(e.Value ?? 0));
        TotalText = total.ToString("N0", CultureInfo.InvariantCulture) + " ₽";
    }
}