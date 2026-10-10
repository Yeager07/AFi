namespace AFi.Models;

/// <summary>
/// Строка легенды для диаграммы: категория, сумма, процент и цвет.
/// Используется для отображения списка категорий под круговой диаграммой.
/// </summary>
public class LegendItem
{
    public string CategoryName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public string PercentText { get; set; } = string.Empty;
    public Color Color { get; set; } = Colors.Gray;
}