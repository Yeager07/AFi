using System.Globalization;

namespace AFi.Converters;

/// <summary>
/// Возвращает 1.0 для включённых элементов и 0.55 для исключённых —
/// чтобы визуально приглушить категории, не попавшие в прогноз.
/// </summary>
public class OpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType,
                          object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? 1.0 : 0.55;
        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType,
                              object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}