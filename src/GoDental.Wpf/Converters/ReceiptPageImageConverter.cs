using System.Globalization;
using System.Windows.Data;
using GoDental.Wpf.Common;

namespace GoDental.Wpf.Converters;

public sealed class ReceiptPageImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is byte[] bytes ? ReceiptPageImage.Load(bytes, preview: true) : null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
