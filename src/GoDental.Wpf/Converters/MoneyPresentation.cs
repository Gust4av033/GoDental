using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GoDental.Wpf.Converters;

// Display only: values and commercial calculations still come from Application.
public sealed class UsdMoneyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is decimal amount ? "$ " + amount.ToString("#,##0.00;-#,##0.00", CultureInfo.InvariantCulture) : "—";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class NegativeAmountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is decimal amount && amount < 0;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}
