using System.Globalization;

namespace AFi.Converters;

/// <summary>
/// Инвертирует булево значение: true → false, false → true.
/// Нужен, когда логика в VM говорит «мы в режиме X», а UI-свойство
/// требует противоположного (например, IsEnabled vs IsEditing).
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType,
                          object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value!;
    }

    public object ConvertBack(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return value!;
    }
}