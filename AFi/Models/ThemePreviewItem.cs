using CommunityToolkit.Mvvm.ComponentModel;

namespace AFi.Models;

/// <summary>
/// Элемент превью темы на экране «Настройки»: ключ, название,
/// цвета-образцы и признак выбора.
/// </summary>
public partial class ThemePreviewItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Цвет фона страницы в этой теме.</summary>
    public Color PreviewBackground { get; set; } = Colors.Gray;

    /// <summary>Цвет карточки в этой теме.</summary>
    public Color PreviewCard { get; set; } = Colors.LightGray;

    /// <summary>Акцентный цвет (кнопки) в этой теме.</summary>
    public Color PreviewPrimary { get; set; } = Colors.Purple;

    /// <summary>Цвет основного текста в этой теме.</summary>
    public Color PreviewText { get; set; } = Colors.Black;

    /// <summary>Выбрана ли эта тема сейчас.</summary>
    [ObservableProperty]
    private bool _isSelected;
}