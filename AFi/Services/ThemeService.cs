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
        ["ShellBackgroundColor"]   = "#1E1E1E",
        ["ShellTextColor"]         = "#FFFFFF",
        ["FlyoutBackgroundColor"]  = "#1E1E1E",
    };

    private static readonly Dictionary<string, string> SpaceTheme = new()
    {
        ["PrimaryColor"]           = "#9B7EE8",
        ["PrimaryTextColor"]       = "#FFFFFF",
        ["PageBackgroundColor"]    = "#0A0E27",   // глубокий космический синий
        ["CardBackgroundColor"]    = "#131938",
        ["CardBorderColor"]        = "#2A3358",
        ["TextPrimaryColor"]       = "#FFFFFF",
        ["TextSecondaryColor"]     = "#8A96C0",
        ["SuccessColor"]           = "#4CAF50",
        ["DangerColor"]            = "#FF6B6B",
        ["WarningColor"]           = "#FFD700",   // звёздное золото
        ["DividerColor"]           = "#2A3358",
        ["ShellBackgroundColor"]   = "#131938",
        ["ShellTextColor"]         = "#FFFFFF",
        ["FlyoutBackgroundColor"]  = "#0A0E27",
    };

    private static readonly Dictionary<string, string> HalloweenTheme = new()
    {
        ["PrimaryColor"]           = "#FF7A1A",   // тыквенный оранжевый
        ["PrimaryTextColor"]       = "#1A0F2E",
        ["PageBackgroundColor"]    = "#1A0F2E",   // тёмно-фиолетовый
        ["CardBackgroundColor"]    = "#241539",
        ["CardBorderColor"]        = "#3D2857",
        ["TextPrimaryColor"]       = "#F5E6D3",   // тёплый белый (как свеча)
        ["TextSecondaryColor"]     = "#A88CC0",
        ["SuccessColor"]           = "#7CB342",   // ядовито-зелёный
        ["DangerColor"]            = "#E53935",
        ["WarningColor"]           = "#FFB300",
        ["DividerColor"]           = "#3D2857",
        ["ShellBackgroundColor"]   = "#241539",
        ["ShellTextColor"]         = "#F5E6D3",
        ["FlyoutBackgroundColor"]  = "#1A0F2E",
    };

    private static readonly Dictionary<string, string> PixelTheme = new()
    {
        ["PrimaryColor"]           = "#00A8FF",   // ярко-синий, «8-бит»
        ["PrimaryTextColor"]       = "#FFFFFF",
        ["PageBackgroundColor"]    = "#1B1B2F",
        ["CardBackgroundColor"]    = "#252540",
        ["CardBorderColor"]        = "#4A4A6E",
        ["TextPrimaryColor"]       = "#FFFFFF",
        ["TextSecondaryColor"]     = "#A0A0C0",
        ["SuccessColor"]           = "#7CB342",
        ["DangerColor"]            = "#FF5252",
        ["WarningColor"]           = "#FFD700",
        ["DividerColor"]           = "#4A4A6E",
        ["ShellBackgroundColor"]   = "#252540",
        ["ShellTextColor"]         = "#FFFFFF",
        ["FlyoutBackgroundColor"]  = "#1B1B2F",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> AllThemes = new()
    {
        ["Light"]     = LightTheme,
        ["Dark"]      = DarkTheme,
        ["Space"]     = SpaceTheme,
        ["Halloween"] = HalloweenTheme,
        ["Pixel"]     = PixelTheme,
    };

    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Light"]     = "Светлая",
        ["Dark"]      = "Тёмная",
        ["Space"]     = "Космос",
        ["Halloween"] = "Хеллоуин",
        ["Pixel"]     = "Пиксельная",
    };

    public IReadOnlyList<string> AvailableThemeKeys { get; } =
        new List<string> { "Light", "Dark", "Space", "Halloween", "Pixel" };

    public string GetDisplayName(string themeKey) =>
        DisplayNames.TryGetValue(themeKey, out var name) ? name : themeKey;

    private static Dictionary<string, string> GetTheme(string name) =>
        AllThemes.TryGetValue(name, out var theme) ? theme : LightTheme;

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