namespace AFi.Models;

/// <summary>
/// Результат расчёта прогноза бюджета на следующий месяц.
/// </summary>
public class ForecastResult
{
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }

    public List<ForecastItem> Items { get; set; } = new();

    /// <summary>
    /// Итоговая сумма прогноза — сумма только включённых категорий.
    /// </summary>
    public decimal TotalPredicted { get; set; }

    /// <summary>
    /// Форматированная итоговая сумма.
    /// </summary>
    public string TotalPredictedText =>
        TotalPredicted.ToString("N0") + " ₽";

    /// <summary>
    /// Текстовое описание периода: "Июль — Сентябрь 2026".
    /// </summary>
    public string PeriodText
    {
        get
        {
            var culture = new System.Globalization.CultureInfo("ru-RU");
            var fromMonth = culture.DateTimeFormat.GetMonthName(PeriodFrom.Month);
            var toMonth = culture.DateTimeFormat.GetMonthName(PeriodTo.Month);
            fromMonth = char.ToUpper(fromMonth[0]) + fromMonth.Substring(1);
            toMonth = char.ToUpper(toMonth[0]) + toMonth.Substring(1);

            if (PeriodFrom.Year == PeriodTo.Year)
                return $"{fromMonth} — {toMonth} {PeriodTo.Year}";
            return $"{fromMonth} {PeriodFrom.Year} — {toMonth} {PeriodTo.Year}";
        }
    }

    /// <summary>
    /// true, если в прогнозе есть хотя бы одна включённая категория.
    /// </summary>
    public bool HasForecast => Items.Any(i => i.IsIncluded);
}