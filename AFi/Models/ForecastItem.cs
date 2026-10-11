namespace AFi.Models;

/// <summary>
/// Строка прогноза: одна категория расходов с предсказанной суммой
/// и признаком, включена ли она в итоговый прогноз.
/// </summary>
public class ForecastItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// В скольких из 3 месяцев встречалась категория (0–3).
    /// </summary>
    public int MonthsPresent { get; set; }

    /// <summary>
    /// Предсказанная сумма за следующий месяц.
    /// Для исключённых категорий (1 месяц) — 0.
    /// </summary>
    public decimal PredictedSum { get; set; }

    /// <summary>
    /// true, если категория включена в итоговый прогноз (встречалась 2+ месяцев).
    /// </summary>
    public bool IsIncluded { get; set; }

    /// <summary>
    /// Название с иконкой: "🏠 Аренда".
    /// </summary>
    public string DisplayName =>
        string.IsNullOrEmpty(Icon) ? CategoryName : $"{Icon} {CategoryName}";

    /// <summary>
    /// Человекочитаемая частота: "встречалась 3 месяца".
    /// </summary>
    public string FrequencyText => MonthsPresent switch
    {
        0 => "нет данных",
        1 => "была 1 месяц",
        2 => "была 2 месяца",
        3 => "была 3 месяца",
        _ => $"была {MonthsPresent} мес.",
    };

    /// <summary>
    /// Форматированная сумма для отображения.
    /// </summary>
    public string PredictedSumText =>
        PredictedSum > 0
            ? PredictedSum.ToString("N0") + " ₽"
            : "—";
}