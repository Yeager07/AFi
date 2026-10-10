namespace AFi.Services;

public interface IThemeService
{
    string ApplyTheme(string themeName);

    string CurrentThemeName { get; }

    void ApplySavedTheme();

    event EventHandler? ThemeChanged;

    IReadOnlyList<string> AvailableThemeKeys { get; }

    string GetDisplayName(string themeKey);

    /// <summary>
    /// Возвращает полный набор цветов темы (ключ → hex).
    /// Используется для построения превью на экране «Настройки».
    /// </summary>
    IReadOnlyDictionary<string, string> GetThemeColors(string themeKey);
}