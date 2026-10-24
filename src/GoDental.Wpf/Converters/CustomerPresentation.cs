using System.Globalization;
using System.Windows.Data;

namespace GoDental.Wpf.Converters;

// Presentación de identidad y datos opcionales; nunca modifica los DTO.
public sealed class CustomerInitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var words = (value as string ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 0 ? "?" : string.Concat(words.Take(2).Select(word => StringInfo.GetNextTextElement(word))).ToUpper(culture);
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class CustomerOptionalTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null || value is string text && string.IsNullOrWhiteSpace(text) ? parameter as string ?? "Sin registrar" : value;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
