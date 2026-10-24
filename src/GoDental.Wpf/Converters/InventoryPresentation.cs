using System.Globalization;
using System.Windows.Data;
using GoDental.Application.DTOs;

namespace GoDental.Wpf.Converters;

// Clasifica únicamente el disponible físico publicado en el catálogo; no autoriza ventas.
public sealed class ProductStockCaptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is ProductData product
        ? product.AvailableStock <= 0 ? "Agotado" : product.AvailableStock <= product.MinimumStock ? "Stock bajo" : "Con stock"
        : "Sin consultar";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InventoryCostConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is decimal cost
        ? "$ " + cost.ToString("#,##0.000000;-#,##0.000000", CultureInfo.InvariantCulture) : "—";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
