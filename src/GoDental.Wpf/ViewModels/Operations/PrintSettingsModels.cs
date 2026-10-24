using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GoDental.Application.DTOs;
namespace GoDental.Wpf.ViewModels.Operations;

public static class PrintMeasure
{
    public static decimal Read(string value, string field)
    {
        if (!decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"{field}: escribe una medida en mm (por ejemplo, 50 o 50.5).");
        return result;
    }
    public static string Text(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
public sealed partial class ReceiptPrintSettingsModel : ObservableObject
{
    public IReadOnlyList<string> Papers { get; } = ["Carta", "A4", "Ticket 80 mm", "Ticket 58 mm", "Personalizado"];
    public IReadOnlyList<string> Kinds { get; } = ["Venta", "Entrega"];
    [ObservableProperty] private string paper = "Carta";
    [ObservableProperty] private string kind = "Venta";
    [ObservableProperty] private string width = "215.9";
    [ObservableProperty] private string height = "279.4";
    [ObservableProperty] private string margin = "11.3";
    partial void OnPaperChanged(string value)
    {
        (Width, Height, Margin) = value switch
        {
            "Carta" => ("215.9", "279.4", "11.3"), "A4" => ("210", "297", "11.3"),
            "Ticket 80 mm" => ("80", "250", "3"), "Ticket 58 mm" => ("58", "250", "3"),
            _ => (Width, Height, Margin)
        };
    }
    public PrintLayout Layout()
    {
        var layout = new PrintLayout(PrintMeasure.Read(Width, "Ancho"), PrintMeasure.Read(Height, "Alto"), PrintMeasure.Read(Margin, "Margen"));
        layout.Validate(narrow: layout.WidthMm < 140); return layout;
    }
    public ReceiptKind DocumentKind => Kind == "Entrega" ? ReceiptKind.Delivery : ReceiptKind.Sale;
    public void Load(PrintPreferences preferences)
    {
        if (preferences.Receipt is not { } layout) return;
        Paper = "Personalizado"; Width = PrintMeasure.Text(layout.WidthMm); Height = PrintMeasure.Text(layout.HeightMm); Margin = PrintMeasure.Text(layout.MarginMm);
        Kind = preferences.Kind == ReceiptKind.Delivery ? "Entrega" : "Venta";
    }
}
public sealed partial class LabelPrintSettingsModel : ObservableObject
{
    public IReadOnlyList<string> Papers { get; } = ["Etiqueta", "Carta", "A4"];
    public IReadOnlyList<string> Presets { get; } = ["50 × 30 mm", "40 × 30 mm", "Personalizado"];
    [ObservableProperty] private string preset = "50 × 30 mm";
    [ObservableProperty] private string paper = "Etiqueta";
    [ObservableProperty] private string width = "50";
    [ObservableProperty] private string height = "30";
    [ObservableProperty] private string copies = "1";
    [ObservableProperty] private string margin = "5";
    [ObservableProperty] private string gap = "2";
    partial void OnPresetChanged(string value)
    {
        if (value == "50 × 30 mm") { Width = "50"; Height = "30"; }
        if (value == "40 × 30 mm") { Width = "40"; Height = "30"; }
    }
    public LabelPrintOptions Options()
    {
        if (!int.TryParse(Copies, out var copies)) throw new ArgumentException("Copias: escribe un número entero de 1 a 200.");
        var result = new LabelPrintOptions(PrintMeasure.Read(Width, "Ancho"), PrintMeasure.Read(Height, "Alto"), copies, Paper,
            PrintMeasure.Read(Margin, "Margen de hoja"), PrintMeasure.Read(Gap, "Separación"));
        result.Validate(); return result;
    }
    public void Load(LabelPrintOptions? value)
    {
        if (value is null) return;
        Preset = "Personalizado"; Paper = value.Paper; Width = PrintMeasure.Text(value.WidthMm); Height = PrintMeasure.Text(value.HeightMm);
        Copies = "1"; Margin = PrintMeasure.Text(value.MarginMm); Gap = PrintMeasure.Text(value.GapMm);
    }
}
