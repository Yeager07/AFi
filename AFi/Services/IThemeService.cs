namespace AFi.Services;

/// <summary>
/// Сервис управления темой оформления.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Применить тему по имени словаря ("Light", "Dark", и т.п.).
    /// </summary>
    string ApplyTheme(string themeName);

    /// <summary>
    /// Текущее имя темы.
    /// </summary>
    string CurrentThemeName { get; }

    /// <summary>
    /// Прочитать сохранённую тему и применить её. Вызывается при старте приложения.
    /// </summary>
    void ApplySavedTheme();

    /// <summary>
    /// Событие: тема была изменена. Подписчики могут обновить UI,
    /// зависящий от темы (например, иконку в Toolbar).
    /// </summary>
    event EventHandler? ThemeChanged;
}