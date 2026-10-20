using System.Text;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace GoDental.Tests.Common;

public sealed class ReceiptDocumentTests
{
    private static ReceiptPdfRenderer Renderer() => new(Options.Create(new ReceiptPdfOptions { Phone = "7203-7117" }));
    private static SaleSnapshot Example(int count = 1) => new(new()
    {
        Id = 1, Number = "VEN-EJEMPLO-001", State = "CONFIRMADA", CustomerId = 1,
        CustomerSnapshot = "{\"Name\":\"Clínica de ejemplo · datos ficticios\",\"Phone\":\"0000-0000\",\"Email\":\"cliente@example.invalid\"}",
        Date = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.FromHours(-6)),
        Subtotal = 18 * count, Total = 18 * count + 5, DeliveryCharge = 5, CashTendered = 18 * count + 7, Change = 2
    }, Enumerable.Range(1, count).Select(i => new SaleLineData
    {
        Sku = $"PRD-EJEMPLO-{i:000}", Name = "Producto odontológico de ejemplo para entrega", Quantity = 3, UnitPrice = 6, Total = 18
    }).ToArray(), [new() { Method = "Efectivo", Amount = 18 * count + 5 }], new()
    {
        Id = 42, Contact = "Contacto de ejemplo", Phone = "0000-0000", Address = "Dirección de entrega de ejemplo, edificio de prueba, segundo nivel",
        Municipality = "Municipio de ejemplo", Department = "Departamento de ejemplo", Reference = "Referencia de prueba, sin datos reales",
        Responsible = "Repartidor de ejemplo", Scheduled = new DateTime(2026, 10, 21)
    });

    [Fact]
    public void PreviewProducesRealPdfAndPrintablePagesFromDeliverySnapshot()
    {
        var source = Example(); var document = Renderer().RenderPreview(source);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(document.Pdf, 0, 5)); Assert.Single(document.Pages);
        Assert.True(document.Pages[0].Length > 1000); Assert.Equal(23, source.Sale.Total); Assert.Equal("Contacto de ejemplo", source.Delivery!.Contact);
        SaveExample("comprobante-ejemplo", document);
    }
    [Fact]
    public void LongNamesAndMultiplePagesDoNotTruncateTheDeliveryDocument()
    {
        var source = Example(45); source.Lines[0].Name = string.Join(" ", Enumerable.Repeat("Descripción extensa de ejemplo", 12));
        source.Delivery!.Reference = new string('R', 280);
        var document = Renderer().RenderPreview(source);
        Assert.True(document.Pages.Count > 1); Assert.All(document.Pages, page => Assert.True(page.Length > 1000));
        SaveExample("comprobante-multipagina", document);
    }
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    public void LegacyCustomerSnapshotsAndSalesWithoutOrderStillGeneratePdf(string json)
    {
        var source = Example() with { Delivery = null }; source.Sale.CustomerSnapshot = json;
        Assert.True(Renderer().Render(source).Length > 1000);
    }
    [Fact]
    public async Task PreviewRequiresConfirmedSaleAndReprintPermission()
    {
        var session = new UserSession(); var repository = new MvpUiRepository();
        session.Begin(new(1, "lector", "Lector", 1, [], ["VENTAS.VER"], false));
        var service = new SaleReceiptService(repository, Renderer(), session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewAsync(1));
        session.Begin(new(1, "lector", "Lector", 1, [], ["VENTAS.VER", "VENTAS.REIMPRIMIR"], false));
        repository.Sale.Sale.State = "BORRADOR";
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(1)); Assert.Empty(repository.Writes);
    }
    private static void SaveExample(string name, ReceiptDocument document)
    {
        var directory = Environment.GetEnvironmentVariable("GODENTAL_PDF_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, name + ".pdf"), document.Pdf);
        for (var i = 0; i < document.Pages.Count; i++)
        {
            var suffix = document.Pages[i][0] == 0x89 ? ".png" : ".jpg";
            File.WriteAllBytes(Path.Combine(directory, $"{name}-{i + 1}{suffix}"), document.Pages[i]);
        }
    }
}
