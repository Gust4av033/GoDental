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
}

public sealed class ReceiptPdfRenderer(IOptions<ReceiptPdfOptions> options) : IReceiptRenderer
{
    public byte[] Render(SaleSnapshot snapshot)
    {
        if (!Enum.TryParse<LicenseType>(options.Value.License, true, out var license) || !Enum.IsDefined(license))
            throw new InvalidOperationException("Configura Pdf:License para generar comprobantes.");
        QuestPDF.Settings.License = license;
        var s = snapshot.Sale;
        var customer = "Cliente";
        if (!string.IsNullOrWhiteSpace(s.CustomerSnapshot))
        {
            using var json = JsonDocument.Parse(s.CustomerSnapshot);
            if (json.RootElement.TryGetProperty("Name", out var name))
                customer = name.GetString() ?? customer;
        }

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(10));
            page.Header().Column(c =>
            {
                c.Item().Text("GoDental · Comprobante interno").FontSize(20).SemiBold();
                c.Item().Text($"{s.Number} · {s.Date:yyyy-MM-dd HH:mm}");
                c.Item().Text(customer);
            });
            page.Content().PaddingVertical(15).Column(c =>
            {
                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });
                    table.Header(h =>
                    {
                        h.Cell().Text("Producto / SKU").SemiBold();
                        h.Cell().Text("Cantidad").SemiBold();
                        h.Cell().Text("Precio neto").SemiBold();
                        h.Cell().Text("Total").SemiBold();
                    });
                    foreach (var l in snapshot.Lines)
                    {
                        table.Cell().PaddingVertical(5).Text($"{l.Name} ({l.Sku})");
                        table.Cell().Text(l.Quantity.ToString("0.####"));
                        table.Cell().Text(l.UnitPrice.ToString("0.00"));
                        table.Cell().Text(l.Total.ToString("0.00"));
                    }
                });
                c.Item().PaddingTop(15).Text($"Subtotal neto: {s.Subtotal:0.00}");
                c.Item().Text($"Descuento: {s.Discount:0.00}");
                c.Item().Text($"Impuestos: {s.Tax:0.00}");
                c.Item().Text($"Entrega: {s.DeliveryCharge:0.00}");
                c.Item().Text($"Total: {s.Total:0.00}").SemiBold();
                foreach (var p in snapshot.Payments)
                    c.Item().Text($"{p.Method}: {p.Amount:0.00}");
                c.Item().Text($"Efectivo recibido: {s.CashTendered:0.00} · Cambio: {s.Change:0.00}");
                c.Item().Text($"Saldo pendiente: {s.Pending:0.00}");
                c.Item().PaddingTop(15).Text("Comprobante operativo interno. No es un DTE aceptado por Hacienda.").SemiBold();
            });
            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Página ");
                t.CurrentPageNumber();
            });
        })).GeneratePdf();
    }
}
