using System.Collections.ObjectModel;
using AFi.Models;
using AFi.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AFi.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public ObservableCollection<ThemePreviewItem> Themes { get; } = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += OnThemeChanged;
        BuildThemes();
    }

    /// <summary>
    /// Строит список превью по всем темам из ThemeService.
    /// </summary>
    private void BuildThemes()
    {
        Themes.Clear();

        foreach (var key in _themeService.AvailableThemeKeys)
        {
            var colors = _themeService.GetThemeColors(key);
            var item = new ThemePreviewItem
            {
                Key = key,
                Name = _themeService.GetDisplayName(key),
                IsSelected = key == _themeService.CurrentThemeName,
            };

            if (colors.TryGetValue("PageBackgroundColor", out var bg))
                item.PreviewBackground = Color.FromArgb(bg);
            if (colors.TryGetValue("CardBackgroundColor", out var card))
                item.PreviewCard = Color.FromArgb(card);
            if (colors.TryGetValue("PrimaryColor", out var primary))
                item.PreviewPrimary = Color.FromArgb(primary);
            if (colors.TryGetValue("TextPrimaryColor", out var text))
                item.PreviewText = Color.FromArgb(text);

            Themes.Add(item);
        }
    }

    /// <summary>
    /// Применить выбранную тему.
    /// </summary>
    [RelayCommand]
    private void SelectTheme(ThemePreviewItem? item)
    {
        if (item is null) return;

        _themeService.ApplyTheme(item.Key);
        StatusMessage = $"Тема «{item.Name}» применена";
    }

    /// <summary>
    /// Обновляет галочку при смене темы (в том числе с других страниц).
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var t in Themes)
            t.IsSelected = t.Key == _themeService.CurrentThemeName;
    }
}