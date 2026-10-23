using System.Globalization;
using System.Text.Json;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GoDental.Infrastructure.Operations;

public sealed class ReceiptPdfOptions
{
    public string License { get; set; } = "Community";
    public string IssuerName { get; set; } = "GoDental";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public sealed partial class ReceiptPdfRenderer(IOptions<ReceiptPdfOptions> options) : IReceiptRenderer
{
    private const string Ink = "#18334D", Blue = "#1474B8", Muted = "#526779", Line = "#DCE5ED", Tint = "#F1F6FA";
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var stream = typeof(ReceiptPdfRenderer).Assembly.GetManifestResourceStream("GoDental.Branding.Logo.png") ?? throw new InvalidOperationException("Falta el logo del comprobante.");
        using var bytes = new MemoryStream(); stream.CopyTo(bytes); return bytes.ToArray();
    });
    private static string Money(decimal value) => value.ToString("$ #,##0.00", Usd);
    public byte[] Render(SaleSnapshot snapshot, PrintLayout? layout = null, ReceiptKind kind = ReceiptKind.Sale) => WithLayoutError(() => Compose(snapshot, layout ?? PrintLayout.Letter, kind).GeneratePdf());
    public ReceiptDocument RenderPreview(SaleSnapshot snapshot, PrintLayout? layout = null, ReceiptKind kind = ReceiptKind.Sale)
    {
        layout ??= PrintLayout.Letter;
        var document = Compose(snapshot, layout, kind);
        return WithLayoutError(() => new ReceiptDocument(document.GeneratePdf(), document.GenerateImages(new ImageGenerationSettings { RasterDpi = 300 }).ToArray(), layout.WidthMm, layout.HeightMm, layout.MarginMm));
    }

    private static T WithLayoutError<T>(Func<T> generate)
    {
        try { return generate(); }
        catch (QuestPDF.Drawing.Exceptions.DocumentLayoutException)
        { throw new ArgumentException("El contenido no cabe con estas medidas. Aumenta el alto/ancho o reduce el margen y actualiza la vista previa."); }
    }
    private IDocument Compose(SaleSnapshot snapshot, PrintLayout layout, ReceiptKind kind)
    {
        if (!Enum.TryParse<LicenseType>(options.Value.License, true, out var license) || !Enum.IsDefined(license))
            throw new InvalidOperationException("Configura Pdf:License para generar comprobantes.");
        QuestPDF.Settings.License = license;
        layout.Validate(narrow: layout.WidthMm < 140);
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Tipo de documento no válido.");
        if (kind == ReceiptKind.Delivery && (snapshot.Delivery is null || snapshot.Sale.State is "DEVUELTA_PARCIAL" or "DEVUELTA_TOTAL" or "ANULADA")) throw new ArgumentException("La nota de entrega requiere una venta vigente con pedido asociado.");
        if (kind == ReceiptKind.Delivery || layout.WidthMm < 140) return ComposeCompact(snapshot, layout, kind);
        var sale = snapshot.Sale;
        var customer = CustomerFields(sale.CustomerSnapshot);
        string Field(string key) => customer.GetValueOrDefault(key) ?? "";
        var name = Field("Name");
        if (string.IsNullOrWhiteSpace(name)) name = sale.CustomerId is null ? "Consumidor final" : "Cliente registrado";
        var issuer = options.Value;
        return Document.Create(document => document.Page(page =>
        {
            page.Size((float)layout.WidthMm, (float)layout.HeightMm, Unit.Millimetre); page.Margin((float)layout.MarginMm, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink));
            page.Header().PaddingBottom(16).BorderBottom(2).BorderColor(Blue).PaddingBottom(12).Row(row =>
            {
                row.RelativeItem(3).Column(c =>
                {
                    c.Item().Row(brand =>
                    {
                        brand.ConstantItem(88).Height(76).Image(Logo.Value).FitArea();
                        brand.RelativeItem().PaddingLeft(10).AlignMiddle().Column(identity =>
                        {
                            identity.Item().Text(string.IsNullOrWhiteSpace(issuer.IssuerName) ? "GoDental" : issuer.IssuerName).FontSize(21).Bold().FontColor(Blue);
                            Optional(identity, issuer.Phone, "Tel. ");
                        });
                    });
                    c.Item().Text("COMPROBANTE DE VENTA Y ENTREGA").FontSize(9).SemiBold().LetterSpacing(.5f);
                    Optional(c, issuer.Address); Optional(c, issuer.Email);
                });
                row.RelativeItem(2).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("COMPROBANTE INTERNO").FontSize(8).FontColor(Muted);
                    c.Item().AlignRight().Text(sale.Number).FontSize(12).Bold();
                    c.Item().AlignRight().Text(sale.Date.ToString("dd/MM/yyyy", Usd));
                    c.Item().PaddingTop(5).AlignRight().Background(Tint).PaddingHorizontal(9).PaddingVertical(4)
                        .Text(sale.State is "ANULADA" or "DEVUELTA_PARCIAL" or "DEVUELTA_TOTAL" ? sale.State : sale.Pending == 0 ? "PAGADO" : "SALDO PENDIENTE").SemiBold().FontColor(Blue);
                });
            });
            page.Content().Column(c =>
            {
                c.Spacing(14);
                c.Item().Row(row =>
                {
                    row.RelativeItem().Border(1).BorderColor(Line).Padding(12).Column(client =>
                    {
                        Heading(client, "CLIENTE"); client.Item().Text(name).FontSize(11).SemiBold();
                        Optional(client, Field("CommercialName")); Optional(client, Field("NIT"), "NIT: "); Optional(client, Field("NRC"), "NRC: ");
                        Optional(client, Field("DUI"), "DUI: "); Optional(client, Field("Phone"), "Tel. "); Optional(client, Field("Email"));
                    });
                    if (snapshot.Delivery is { } delivery)
                    {
                        row.ConstantItem(12);
                        row.RelativeItem().Background(Tint).Padding(12).Column(info =>
                        {
                            Heading(info, $"ENTREGA · PEDIDO #{delivery.Id}");
                            Optional(info, delivery.Contact, "Recibe: "); Optional(info, delivery.Phone, "Tel. "); Optional(info, delivery.Address);
                            Optional(info, string.Join(", ", new[] { delivery.District, delivery.Municipality, delivery.Department }.Where(x => !string.IsNullOrWhiteSpace(x))));
                            Optional(info, delivery.Reference, "Referencia: "); Optional(info, delivery.Scheduled?.ToString("dd/MM/yyyy", Usd), "Prevista: ");
                            Optional(info, delivery.Responsible, "Responsable: ");
                        });
                    }
                });
                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3.5f); columns.RelativeColumn(.7f); columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.1f); columns.RelativeColumn(1.1f); columns.RelativeColumn(1.3f);
                    });
                    table.Header(header =>
                    {
                        foreach (var title in new[] { "PRODUCTO", "CANT.", "P. NETO", "DESC.", "IMPUESTO", "IMPORTE" })
                        {
                            var cell = header.Cell().Background(Ink).Padding(7);
                            (title == "PRODUCTO" ? cell.AlignLeft() : cell.AlignRight()).Text(title).FontSize(7).SemiBold().FontColor(Colors.White);
                        }
                    });
                    foreach (var item in snapshot.Lines)
                    {
                        table.Cell().Element(Cell).Column(product =>
                        {
                            product.Item().Text(item.Name).SemiBold();
                        });
                        table.Cell().Element(Cell).AlignRight().Text(item.Quantity.ToString("0.####", Usd));
                        table.Cell().Element(Cell).AlignRight().Text(Money(item.UnitPrice)); table.Cell().Element(Cell).AlignRight().Text(Money(item.Discount));
                        table.Cell().Element(Cell).AlignRight().Text(Money(item.Tax)); table.Cell().Element(Cell).AlignRight().Text(Money(item.Total)).SemiBold();
                    }
                });
                if(sale.State is "DEVUELTA_PARCIAL" or "DEVUELTA_TOTAL" or "ANULADA") c.Item().Text("ESTADO: " + sale.State + " · Importes originales; consultar posventa para compensaciones.").Bold();
                c.Item().Text("Precios y subtotal netos. Los descuentos informados ya están aplicados.").FontSize(7).FontColor(Muted);
                c.Item().Row(row =>
                {
                    row.RelativeItem(3).Column(payments =>
                    {
                        Heading(payments, "PAGOS REGISTRADOS");
                        foreach (var payment in snapshot.Payments)
                        {
                            payments.Item().PaddingBottom(4).Row(p =>
                            {
                                p.RelativeItem().Text(payment.Method); p.ConstantItem(85).AlignRight().Text(Money(payment.Amount)).SemiBold();
                            });
                            if (!string.IsNullOrWhiteSpace(payment.Reference)) payments.Item().PaddingBottom(5).Text("Referencia: " + payment.Reference).FontSize(8).FontColor(Muted);
                        }
                        payments.Item().PaddingTop(6).Column(cash =>
                        {
                            TotalRow(cash, "Efectivo recibido", sale.CashTendered); TotalRow(cash, "Cambio", sale.Change); TotalRow(cash, "Saldo pendiente", sale.Pending);
                        });
                        payments.Item().PaddingTop(5).Text("Moneda: USD · Importes de la venta confirmada.").FontSize(8).FontColor(Muted);
                    });
                    row.ConstantItem(24);
                    row.RelativeItem(2).Background(Tint).Padding(12).Column(totals =>
                    {
                        Heading(totals, "RESUMEN DE VENTA");
                        TotalRow(totals, "Subtotal neto", sale.Subtotal); TotalRow(totals, "Descuento registrado", sale.Discount);
                        TotalRow(totals, "Impuestos", sale.Tax); TotalRow(totals, "Entrega", sale.DeliveryCharge);
                        totals.Item().PaddingTop(9).BorderTop(1).BorderColor(Line).PaddingTop(9).Row(total =>
                        {
                            total.RelativeItem().AlignMiddle().Text("TOTAL").SemiBold();
                            total.RelativeItem(2).AlignRight().Text(Money(sale.Total)).FontSize(19).Bold().FontColor(Blue);
                        });
                    });
                });
                c.Item().PaddingTop(8).BorderTop(1).BorderColor(Line).PaddingTop(10).Column(received =>
                {
                    received.Item().Text(snapshot.Delivery is null ? "RECEPCIÓN DE PRODUCTOS" : "RECEPCIÓN DEL PEDIDO").FontSize(8).SemiBold().FontColor(Muted);
                    received.Item().PaddingTop(16).Text("Recibido por: __________________________    Firma: __________________    Fecha: ______________").FontSize(8);
                    received.Item().PaddingTop(6).Text("La firma acredita recepción física; no sustituye un registro de pago.").FontSize(7).FontColor(Muted);
                });
            });
            page.Footer().PaddingTop(12).BorderTop(1).BorderColor(Line).PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text("Comprobante operativo interno. No es un DTE aceptado por Hacienda.").FontSize(7).FontColor(Muted);
                row.ConstantItem(85).AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(t => t.FontSize(8).FontColor(Muted)); text.Span("Página "); text.CurrentPageNumber(); text.Span(" de "); text.TotalPages();
                });
            });
        }));
    }

    private static IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Line).PaddingVertical(9).PaddingHorizontal(7);
    private static void Heading(ColumnDescriptor c, string title) => c.Item().PaddingBottom(7).Text(title).FontSize(8).SemiBold().FontColor(Muted);
    private static void Optional(ColumnDescriptor c, string? value, string prefix = "")
    {
        if (!string.IsNullOrWhiteSpace(value)) c.Item().PaddingTop(3).Text(prefix + value).FontSize(8);
    }
    private static void TotalRow(ColumnDescriptor c, string label, decimal value) => c.Item().PaddingBottom(5).Row(row =>
    {
        row.RelativeItem().Text(label); row.ConstantItem(85).AlignRight().Text(Money(value));
    });
    private static Dictionary<string, string> CustomerFields(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return [];
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String) fields[property.Name] = property.Value.GetString() ?? "";
            return fields;
        }
        catch (JsonException) { return []; }
    }
}
