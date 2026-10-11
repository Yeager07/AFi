using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace AFi.Models;

/// <summary>
/// Позиция чека — одна строка из кассового чека или списка покупок.
/// Например: «Молоко 2 шт × 89 ₽» или «Хлеб 1 шт × 45 ₽».
/// </summary>
[Table("transaction_items")]
public partial class TransactionItem : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Id операции, к которой привязана позиция.
    /// </summary>
    [Indexed]
    public int TransactionId { get; set; }

    [NotNull, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Количество (может быть дробным — например, 0.5 кг).
    /// </summary>
    public decimal Quantity { get; set; } = 1m;

    /// <summary>
    /// Цена за единицу. Общая стоимость позиции = Quantity × UnitPrice.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Порядок отображения в списке.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Общая стоимость позиции — вычисляется.
    /// </summary>
    [Ignore]
    public decimal Total => Quantity * UnitPrice;

    /// <summary>
    /// Форматированное отображение: «2 × 89 ₽ = 178 ₽».
    /// </summary>
    [Ignore]
    public string SummaryText
    {
        get
        {
            var qtyText = Quantity == Math.Floor(Quantity)
                ? ((int)Quantity).ToString()
                : Quantity.ToString("0.##");

            return $"{qtyText} × {UnitPrice:N0} ₽";
        }
    }

    /// <summary>
    /// Форматированная общая стоимость: «178 ₽».
    /// </summary>
    [Ignore]
    public string TotalText => $"{Total:N0} ₽";
}