using System.Globalization;
using System.Windows.Data;

namespace GoDental.Wpf.Converters;

// Sólo composición visual: adapta columnas al ancho y a los indicadores autorizados.
public sealed class HomeColumnsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var width = values.ElementAtOrDefault(0) is double value ? value : 0;
        var count = int.TryParse(values.ElementAtOrDefault(1)?.ToString(), out var items) ? items : 1;
        var maximum = parameter?.ToString() == "2" ? 2 : 4;
        var desired = maximum == 2 ? (width < 900 ? 1 : 2) : (width < 720 ? 2 : 4);
        return Math.Max(1, Math.Min(count, desired));
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
