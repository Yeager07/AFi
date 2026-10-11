namespace AFi.Models;

/// <summary>
/// Агрегированная позиция для топа — одна строка в развёрнутой
/// карточке легенды. Складывает одинаковые позиции из разных чеков.
/// </summary>
public class TopItem
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Суммарно потрачено на эту позицию за период.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Сколько раз встречалась в разных чеках.
    /// </summary>
    public int PurchaseCount { get; set; }

    public string TotalAmountText => TotalAmount.ToString("N0") + " ₽";

    public string FrequencyText => PurchaseCount switch
    {
        0 => string.Empty,
        1 => "1 покупка",
        2 or 3 or 4 => $"{PurchaseCount} покупки",
        _ when PurchaseCount % 100 >= 11 && PurchaseCount % 100 <= 14 => $"{PurchaseCount} покупок",
        _ when PurchaseCount % 10 == 1 => $"{PurchaseCount} покупка",
        _ when PurchaseCount % 10 >= 2 && PurchaseCount % 10 <= 4 => $"{PurchaseCount} покупки",
        _ => $"{PurchaseCount} покупок",
    };
}