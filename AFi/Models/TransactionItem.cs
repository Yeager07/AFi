using System.Globalization;
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

    [Indexed]
    public int TransactionId { get; set; }

    [NotNull, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Количество (может быть дробным — например, 0.5 кг).
    /// </summary>
    public decimal Quantity { get; set; } = 1m;

    /// <summary>
    /// Цена за единицу.
    /// </summary>
    public decimal UnitPrice { get; set; }

    public int SortOrder { get; set; }

    // ==================== Строковые обёртки для Entry ====================
    // Геттеры возвращают пустую строку, если пользователь ещё не вводил
    // значение. Так поля пустые при создании новой позиции и не
    // подставляют дефолтные "1" и "0".

    private string? _quantityText;
    private string? _unitPriceText;

    [Ignore]
    public string QuantityText
    {
        get => _quantityText ?? string.Empty;
        set
        {
            if (SetProperty(ref _quantityText, value))
            {
                Quantity = ParseDecimal(value, fallback: Quantity);
                NotifyTotals();
            }
        }
    }

    [Ignore]
    public string UnitPriceText
    {
        get => _unitPriceText ?? string.Empty;
        set
        {
            if (SetProperty(ref _unitPriceText, value))
            {
                UnitPrice = ParseDecimal(value, fallback: UnitPrice);
                NotifyTotals();
            }
        }
    }

    /// <summary>
    /// Заполнить строковые поля из текущих значений Quantity и UnitPrice.
    /// Вызывается после загрузки позиции из БД или снапшота,
    /// чтобы пользователь видел текущие числа в полях.
    /// Для новых позиций вызывать не нужно — поля останутся пустыми.
    /// </summary>
    public void InitializeTexts()
    {
        _quantityText = FormatDecimal(Quantity);
        _unitPriceText = FormatDecimal(UnitPrice);

        OnPropertyChanged(nameof(QuantityText));
        OnPropertyChanged(nameof(UnitPriceText));
    }

    // ==================== Вычисляемые ====================

    [Ignore]
    public decimal Total => Quantity * UnitPrice;

    [Ignore]
    public string TotalText => $"{Total:N0} ₽";

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

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private static string FormatDecimal(decimal value)
    {
        return value == Math.Floor(value)
            ? ((int)value).ToString()
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static decimal ParseDecimal(string? text, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var normalized = text.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number,
                                CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}