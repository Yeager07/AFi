using CommunityToolkit.Mvvm.ComponentModel;

namespace AFi.Models;

/// <summary>
/// Строка легенды для диаграммы: категория, сумма, процент и цвет.
/// Может разворачиваться для показа топа позиций чека.
/// </summary>
public partial class LegendItem : ObservableObject
{
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public string PercentText { get; set; } = string.Empty;
    public Color Color { get; set; } = Colors.Gray;

    /// <summary>
    /// Развёрнута ли карточка для показа топа позиций.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// true, если у категории есть агрегированные позиции для показа.
    /// </summary>
    [ObservableProperty]
    private bool _hasTopItems;

    /// <summary>
    /// Топ позиций по расходам внутри этой категории.
    /// Отсортированы по убыванию суммы.
    /// </summary>
    public List<TopItem> TopItems { get; set; } = new();
}