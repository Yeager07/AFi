using CommunityToolkit.Mvvm.ComponentModel;

namespace AFi.Models;

/// <summary>
/// Элемент превью темы на экране «Настройки».
/// Содержит цвета всех основных ролей для построения миниатюры интерфейса.
/// </summary>
public partial class ThemePreviewItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Color PreviewBackground { get; set; } = Colors.Gray;
    public Color PreviewCard { get; set; } = Colors.LightGray;
    public Color PreviewPrimary { get; set; } = Colors.Purple;
    public Color PreviewText { get; set; } = Colors.Black;
    public Color PreviewSecondary { get; set; } = Colors.Gray;

    [ObservableProperty]
    private bool _isSelected;
}