namespace AFi.Services;

/// <summary>
/// Сервис управления темой оформления.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Применить тему по ключу ("Light", "Dark", "Space", "Halloween", "Pixel").
    /// </summary>
    string ApplyTheme(string themeName);

    /// <summary>
    /// Текущий ключ темы.
    /// </summary>
    string CurrentThemeName { get; }

    /// <summary>
    /// Прочитать сохранённую тему и применить её. Вызывается при старте приложения.
    /// </summary>
    void ApplySavedTheme();

    /// <summary>
    /// Событие: тема была изменена.
    /// </summary>
    event EventHandler? ThemeChanged;

    /// <summary>
    /// Список всех доступных ключей тем в порядке отображения.
    /// </summary>
    IReadOnlyList<string> AvailableThemeKeys { get; }

    /// <summary>
    /// Возвращает человекочитаемое название темы по её ключу
    /// ("Light" → "Светлая").
    /// </summary>
    string GetDisplayName(string themeKey);
}