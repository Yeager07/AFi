namespace AFi.Models;

/// <summary>
/// Одна строка в плоском списке Истории. Может быть либо
/// заголовком дня (IsHeader = true), либо транзакцией.
/// Заменяет сгруппированный CollectionView на плоский список
/// с DataTemplateSelector — это убирает лаги прокрутки в MAUI.
/// </summary>
public class HistoryRow
{
    public bool IsHeader { get; set; }

    // ==================== Поля заголовка ====================
    public string DateLabel { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public bool IsPositive { get; set; }

    // ==================== Поле транзакции ====================
    public Transaction? Transaction { get; set; }
}