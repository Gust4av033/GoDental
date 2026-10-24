using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
using GoDental.Application.DTOs;

namespace GoDental.Wpf.Converters;

// El nombre histórico procede del snapshot de la venta, no del cliente actual.
public sealed class SaleCustomerNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SaleData sale) return "—";
        try
        {
            if (!string.IsNullOrWhiteSpace(sale.CustomerSnapshot))
            {
                using var json = JsonDocument.Parse(sale.CustomerSnapshot);
                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("Name", out var name)
                    && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString())) return name.GetString()!;
            }
        }
        catch (JsonException) { }
        return sale.CustomerId is null ? "Consumidor final" : "Cliente registrado";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
