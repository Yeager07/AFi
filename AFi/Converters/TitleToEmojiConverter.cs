using System.Globalization;

namespace AFi.Converters;

public class TitleToEmojiConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["Главная"]     = "🏠",
        ["История"]     = "📜",
        ["Статистика"]  = "📊",
        ["Категории"]   = "🏷️",
        ["Настройки"]   = "⚙️",
    };

    public object Convert(object? value, Type targetType,
                          object? parameter, CultureInfo culture)
    {
        var title = value?.ToString() ?? string.Empty;
        return Map.TryGetValue(title, out var emoji) ? emoji : "•";
    }

    public object ConvertBack(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}