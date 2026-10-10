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
        // Палитра в стиле Interstellar / NASA:
        // глубокий холодный космос, голубой акцент (океанская планета),
        // сдержанные бирюзовые и оранжевые оттенки.
        ["PrimaryColor"]           = "#64B5F6",   // холодный голубой
        ["PrimaryTextColor"]       = "#0B1426",   // тёмный на голубом
        ["PageBackgroundColor"]    = "#0B1426",   // глубокий тёмно-синий
        ["CardBackgroundColor"]    = "#16233D",   // тёмно-синие карточки
        ["CardBorderColor"]        = "#2C4166",   // холодный сине-серый
        ["TextPrimaryColor"]       = "#E8EFF7",   // почти белый
        ["TextSecondaryColor"]     = "#8FA3BD",   // холодный серо-синий
        ["SuccessColor"]           = "#4DB6AC",   // бирюзовый
        ["DangerColor"]            = "#EF5350",   // холодный красный
        ["WarningColor"]           = "#FFA726",   // оранжевый
        ["DividerColor"]           = "#2C4166",
        ["ShellBackgroundColor"]   = "#16233D",
        ["ShellTextColor"]         = "#E8EFF7",
        ["FlyoutBackgroundColor"]  = "#0B1426",
    };

    private static readonly Dictionary<string, string> HalloweenTheme = new()
    {
        // Тыква, костёр, ядовитый котёл, «Кошмар перед Рождеством».
        ["PrimaryColor"]           = "#FF6D00",   // тыква, ярче
        ["PrimaryTextColor"]       = "#0D0614",   // угольно-чёрный на оранжевом
        ["PageBackgroundColor"]    = "#0D0614",   // угольно-фиолетовый
        ["CardBackgroundColor"]    = "#1F0E33",   // тёмно-фиолетовые карточки
        ["CardBorderColor"]        = "#3D2357",
        ["TextPrimaryColor"]       = "#FFE8C8",   // цвет свечи
        ["TextSecondaryColor"]     = "#9B7FB8",   // бледно-фиолетовый
        ["SuccessColor"]           = "#9CCC65",   // ядовито-зелёный
        ["DangerColor"]            = "#C62828",   // кровь
        ["WarningColor"]           = "#FFB300",
        ["DividerColor"]           = "#3D2357",
        ["ShellBackgroundColor"]   = "#1F0E33",
        ["ShellTextColor"]         = "#FFE8C8",
        ["FlyoutBackgroundColor"]  = "#0D0614",
    };

    private static readonly Dictionary<string, string> ArcadeTheme = new()
    {
        ["PrimaryColor"]           = "#FFD700",
        ["PrimaryTextColor"]       = "#000000",
        ["PageBackgroundColor"]    = "#000000",
        ["CardBackgroundColor"]    = "#0F0F2E",
        ["CardBorderColor"]        = "#2121DE",
        ["TextPrimaryColor"]       = "#FFFFFF",
        ["TextSecondaryColor"]     = "#8B8BC0",
        ["SuccessColor"]           = "#00E5FF",
        ["DangerColor"]            = "#FF3030",
        ["WarningColor"]           = "#FFB852",
        ["DividerColor"]           = "#2121DE",
        ["ShellBackgroundColor"]   = "#0F0F2E",
        ["ShellTextColor"]         = "#FFFFFF",
        ["FlyoutBackgroundColor"]  = "#000000",
    };

    private static readonly Dictionary<string, Dictionary<string, string>> AllThemes = new()
    {
        ["Light"]     = LightTheme,
        ["Dark"]      = DarkTheme,
        ["Space"]     = SpaceTheme,
        ["Halloween"] = HalloweenTheme,
        ["Arcade"]    = ArcadeTheme,
    };

    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["Light"]     = "Светлая",
        ["Dark"]      = "Тёмная",
        ["Space"]     = "Космос",
        ["Halloween"] = "Хеллоуин",
        ["Arcade"]    = "Аркадная",
    };

    public IReadOnlyList<string> AvailableThemeKeys { get; } =
        new List<string> { "Light", "Dark", "Space", "Halloween", "Arcade" };

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

    public IReadOnlyDictionary<string, string> GetThemeColors(string themeKey)
    {
        return GetTheme(themeKey);
    }
}