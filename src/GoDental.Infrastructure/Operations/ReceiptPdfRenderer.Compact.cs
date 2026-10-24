using GoDental.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace GoDental.Infrastructure.Operations;

public sealed partial class ReceiptPdfRenderer
{
    private IDocument ComposeCompact(SaleSnapshot snapshot, PrintLayout layout, ReceiptKind kind)
    {
        var deliveryOnly = kind == ReceiptKind.Delivery;
        var sale = snapshot.Sale;
        var fields = CustomerFields(sale.CustomerSnapshot);
        var customer = fields.GetValueOrDefault("Name");
        return Document.Create(d => d.Page(page =>
        {
            page.Size((float)layout.WidthMm, (float)layout.HeightMm, Unit.Millimetre);
            page.Margin((float)layout.MarginMm, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(layout.WidthMm < 70 ? 8 : 9).FontColor(Ink));
            page.Header().PaddingBottom(8).BorderBottom(1).BorderColor(Line).Column(c =>
            {
                c.Item().Text(options.Value.IssuerName).Bold().FontSize(14);
                c.Item().Text(deliveryOnly ? "NOTA INTERNA DE ENTREGA" : "COMPROBANTE INTERNO").SemiBold();
                c.Item().Text(sale.Number).Bold();
                if(sale.State is "DEVUELTA_PARCIAL" or "DEVUELTA_TOTAL" or "ANULADA") c.Item().Text("ESTADO: "+sale.State+" · Importes originales.").Bold();
                c.Item().Text(sale.Date.ToString("dd/MM/yyyy HH:mm", Usd));
                Optional(c, options.Value.Address); Optional(c, options.Value.Phone, "Tel. "); Optional(c, options.Value.Email);
            });
            page.Content().PaddingVertical(8).Column(c =>
            {
                c.Spacing(6);
                c.Item().Text("Cliente: " + (string.IsNullOrWhiteSpace(customer) ? "Consumidor final" : customer)).SemiBold();
                if (snapshot.Delivery is { } delivery)
                {
                    c.Item().Text($"Pedido #{delivery.Id}").Bold();
                    Optional(c, delivery.Contact, "Recibe: "); Optional(c, delivery.Phone, "Tel. ");
                    Optional(c, delivery.Address);
                    Optional(c, string.Join(", ", new[] { delivery.District, delivery.Municipality, delivery.Department }.Where(x => !string.IsNullOrWhiteSpace(x))));
                    Optional(c, delivery.Reference, "Referencia: "); Optional(c, delivery.Responsible, "Responsable: ");
                    Optional(c, delivery.Scheduled?.ToString("dd/MM/yyyy", Usd), "Prevista: ");
                }
                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols => { cols.RelativeColumn(); });
                    table.Header(h => h.Cell().BorderBottom(1).BorderColor(Ink).PaddingVertical(5).Text(deliveryOnly ? "PRODUCTOS A ENTREGAR" : "PRODUCTOS · USD").Bold());
                    foreach (var item in snapshot.Lines)
                        table.Cell().ShowEntire().BorderBottom(.5f).BorderColor(Line).PaddingVertical(5).Column(product =>
                        {
                            product.Item().Text(item.Name).SemiBold();
                            product.Item().Text(item.Sku).FontSize(7);
                            product.Item().Text("Cantidad: " + item.Quantity.ToString("0.####", Usd));
                            if (!deliveryOnly)
                            {
                                product.Item().Text($"P. neto: {Money(item.UnitPrice)} · Desc.: {Money(item.Discount)}").FontSize(7);
                                product.Item().Text($"Impuesto: {Money(item.Tax)} · Importe: {Money(item.Total)}").FontSize(7);
                            }
                        });
                });
                if (!deliveryOnly)
                {
                    c.Item().Text("Precios netos; descuentos ya aplicados.").FontSize(7);
                    c.Item().ShowEntire().Column(totals =>
                    {
                        CompactTotal(totals, "Subtotal neto", sale.Subtotal); CompactTotal(totals, "Descuento", sale.Discount);
                        CompactTotal(totals, "Impuestos", sale.Tax); CompactTotal(totals, "Entrega", sale.DeliveryCharge);
                        CompactTotal(totals, "TOTAL USD", sale.Total); CompactTotal(totals, "Saldo pendiente", sale.Pending);
                    });
                    c.Item().Text("PAGOS REGISTRADOS").SemiBold();
                    foreach (var payment in snapshot.Payments)
                    {
                        c.Item().Text(payment.Method + ": " + Money(payment.Amount));
                        Optional(c, payment.Reference, "Referencia: ");
                    }
                    c.Item().Column(cash => { CompactTotal(cash, "Efectivo recibido", sale.CashTendered); CompactTotal(cash, "Cambio", sale.Change); });
                }
                c.Item().PaddingTop(10).Text("Recibido por: __________________");
                c.Item().Text("Firma: _______________________");
                c.Item().Text("Fecha: _______________________");
                c.Item().Text("La firma acredita recepción física; no registra un pago.").FontSize(7);
            });
            page.Footer().BorderTop(.5f).BorderColor(Line).PaddingTop(4).Column(c =>
            {
                c.Item().Text(deliveryOnly ? "Nota operativa interna. No es guía de transportista ni documento fiscal." : "Comprobante operativo interno. No es un DTE aceptado por Hacienda.").FontSize(6);
                c.Item().Text(t => { t.DefaultTextStyle(s => s.FontSize(7)); t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages(); });
            });
        }));
    }
    private static void CompactTotal(ColumnDescriptor c, string label, decimal value) => c.Item().PaddingBottom(3).Row(r =>
    {
        r.RelativeItem().Text(label); r.RelativeItem().AlignRight().Text(Money(value)).SemiBold();
    });
}
