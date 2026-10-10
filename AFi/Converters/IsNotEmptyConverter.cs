using System.Globalization;

namespace AFi.Converters;

/// <summary>
/// Возвращает true, если строка не пустая и не null.
/// Используется для скрытия элементов, у которых нет текста.
/// </summary>
public class IsNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType,
                          object? parameter, CultureInfo culture)
    {
        return value is string s && !string.IsNullOrWhiteSpace(s);
    }

    public object ConvertBack(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}