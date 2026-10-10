using System.Diagnostics;
using System.Text;

namespace AFi.Services;

public class ThemeService : IThemeService
{
    private const string PreferenceKey = "app_theme";
    private const string DefaultTheme = "Light";

    public string CurrentThemeName { get; private set; } = DefaultTheme;

    public event EventHandler? ThemeChanged;

    // ==================== Определения тем ====================
    // Ключ → hex-значение цвета. Ключи одинаковые во всех темах.

    private static readonly Dictionary<string, string> LightTheme = new()
    {
        ["PrimaryColor"]           = "#512BD4",
        ["PrimaryTextColor"]       = "#FFFFFF",

        ["PageBackgroundColor"]    = "#F3F3F3",
        ["CardBackgroundColor"]    = "#FFFFFF",
        ["CardBorderColor"]        = "#D0D0D0",

        ["TextPrimaryColor"]       = "#000000",
        ["TextSecondaryColor"]     = "#808080",

        ["SuccessColor"]           = "#2E7D32",
        ["DangerColor"]            = "#D32F2F",
        ["WarningColor"]           = "#FFB300",

        ["DividerColor"]           = "#E0E0E0",

        // Shell (верхняя панель и бургер-меню)
        ["ShellBackgroundColor"]   = "#FFFFFF",
        ["ShellTextColor"]         = "#000000",
        ["FlyoutBackgroundColor"]  = "#FFFFFF",
    };

    private static readonly Dictionary<string, string> DarkTheme = new()
    {
        ["PrimaryColor"]           = "#8B7BD8",
        ["PrimaryTextColor"]       = "#FFFFFF",

        ["PageBackgroundColor"]    = "#1A1A1A",
        ["CardBackgroundColor"]    = "#1E1E1E",
        ["CardBorderColor"]        = "#333333",

        ["TextPrimaryColor"]       = "#FFFFFF",
        ["TextSecondaryColor"]     = "#999999",

        ["SuccessColor"]           = "#4CAF50",
        ["DangerColor"]            = "#E57373",
        ["WarningColor"]           = "#FFC107",

        ["DividerColor"]           = "#333333",

        // Shell (верхняя панель и бургер-меню)
        ["ShellBackgroundColor"]   = "#1E1E1E",
        ["ShellTextColor"]         = "#FFFFFF",
        ["FlyoutBackgroundColor"]  = "#1E1E1E",
    };

    private static Dictionary<string, string> GetTheme(string name) =>
        name switch
        {
            "Dark" => DarkTheme,
            _ => LightTheme,
        };

    // ==================== Применение ====================

    public string ApplyTheme(string themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
            themeName = DefaultTheme;

        var diag = new StringBuilder();
        diag.AppendLine($"Тема: {themeName}");

        var app = Application.Current;
        if (app is null)
        {
            diag.AppendLine("Application.Current = null");
            return diag.ToString();
        }

        var theme = GetTheme(themeName);
        var res = app.Resources;

        int added = 0;
        int updated = 0;

        foreach (var kvp in theme)
        {
            var color = Color.FromArgb(kvp.Value);

            if (res.ContainsKey(kvp.Key))
            {
                res[kvp.Key] = color;
                updated++;
            }
            else
            {
                res.Add(kvp.Key, color);
                added++;
            }
        }

        CurrentThemeName = themeName;
        Preferences.Set(PreferenceKey, themeName);

        diag.AppendLine($"Добавлено ключей: {added}");
        diag.AppendLine($"Обновлено ключей: {updated}");
        diag.AppendLine($"Всего ключей в Resources: {res.Count}");

        // Уведомляем подписчиков (например, CategoriesPage для смены иконки)
        ThemeChanged?.Invoke(this, EventArgs.Empty);

        return diag.ToString();
    }

    public void ApplySavedTheme()
    {
        var saved = Preferences.Get(PreferenceKey, DefaultTheme);
        var result = ApplyTheme(saved);
        Debug.WriteLine($"[ThemeService] ApplySavedTheme: {result}");
    }
}