namespace AFi.Models;

/// <summary>
/// Снапшот состояния формы операции. Сохраняется перед вызовом
/// камеры или галереи, чтобы восстановить форму, если система
/// убьёт процесс приложения в фоне.
/// </summary>
public class FormSnapshot
{
    public string AmountText { get; set; } = string.Empty;
    public int SelectedTypeIndex { get; set; }
    public int? CategoryId { get; set; }
    public DateTime Date { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? ReceiptFileName { get; set; }
    public int? EditingTransactionId { get; set; }
    public string? OriginalReceiptFileName { get; set; }
    public List<ItemSnapshot> Items { get; set; } = new();
}

public class ItemSnapshot
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}