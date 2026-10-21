using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Security;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Options;
using StbImageSharp;
using ZXing;
namespace GoDental.Tests.Common;

public sealed class PrintingTests
{
    private static BarcodeLabelPdfRenderer Labels() => new(Options.Create(new ReceiptPdfOptions()));
    [Theory]
    [InlineData("GD0000000001", "CODE128", 40)]
    [InlineData("GD0000000001", "CODE128", 50)]
    [InlineData("5901234123457", "EAN13", 50)]
    [InlineData("036000291452", "UPC", 50)]
    public void RealRenderedLabelDecodesToOriginalValue(string code, string type, int width)
    {
        var document = Labels().RenderPreview(new("Producto de ejemplo", "PRD-0001", code, type), new(WidthMm: width));
        Assert.Single(document.Pages); Assert.True(document.IsLabel); Dimensions(document, width, 30);
        var image = ImageResult.FromMemory(document.Pages.Single(), ColorComponents.RedGreenBlue);
        var source = new RGBLuminanceSource(image.Data, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new() { TryHarder = true } };
        var result = reader.Decode(source);
        Assert.NotNull(result);
        // ZXing puede normalizar UPC-A como EAN13 con un cero inicial.
        Assert.True(result.Text == code || type == "UPC" && result.Text == "0" + code, result.Text);
        Save($"etiqueta-{type}-{width}", document);
    }
    [Fact] public void SheetLayoutUsesPhysicalDimensionsCopiesAndPagination()
    {
        var document = Labels().RenderPreview(new("Producto " + new string('L', 150), "PRD", "GD0000000001", "CODE128"), new(Copies: 25, Paper: "Carta"));
        Assert.Equal(2, document.Pages.Count); Dimensions(document, 215.9m, 279.4m); Save("etiquetas-carta", document);
        var a4 = Labels().RenderPreview(new("Producto", "PRD", "GD0000000001", "CODE128"), new(Copies: 2, Paper: "A4"));
        Dimensions(a4, 210, 297);
    }
    [Theory]
    [InlineData("OTRO", "abc", 50)]
    [InlineData("EAN13", "5901234123458", 50)]
    [InlineData("CODE128", "GD0000000001", 20)]
    [InlineData("CODE128", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", 50)]
    public void UnsupportedInvalidAndTooSmallLabelsAreRejected(string type, string code, int width)
        => Assert.Throws<ArgumentException>(() => Labels().RenderPreview(new("P", "SKU", code, type), new(WidthMm: width)));
    [Theory]
    [InlineData(58, 150)] [InlineData(80, 200)] [InlineData(210, 297)] [InlineData(215.9, 279.4)]
    public void ReceiptsAndDeliveryUseRequestedSizeAndPaginate(double width, double height)
    {
        var layout = new PrintLayout((decimal)width, (decimal)height, 3);
        var renderer = new ReceiptPdfRenderer(Options.Create(new ReceiptPdfOptions()));
        var source = new MvpUiRepository().Sale; source.Sale.Date = new DateTimeOffset(2026, 10, 21, 15, 30, 0, TimeSpan.FromHours(-6));
        var lines = Enumerable.Range(0, 22).Select(i => new SaleLineData { Name = $"Descripción de producto extensa {i} para comprobar el salto de línea", Sku = "PRD-001", Quantity = 1, UnitPrice = 10, Total = 10 }).ToArray();
        source = source with { Lines = lines, Delivery = new() { Id = 1, Contact = "Contacto de ejemplo", Address = "Dirección de ejemplo", Reference = new string('R', 280) } };
        foreach (var kind in new[] { ReceiptKind.Sale, ReceiptKind.Delivery })
        {
            var document = renderer.RenderPreview(source, layout, kind);
            Dimensions(document, layout.WidthMm, layout.HeightMm); Assert.True(document.Pages.Count > 1);
            Save($"{kind}-{width}", document);
        }
    }
    [Fact] public void DeliveryRequiresOrderAndPrintSettingsRejectInvalidMeasures()
    {
        var renderer = new ReceiptPdfRenderer(Options.Create(new ReceiptPdfOptions()));
        Assert.Throws<ArgumentException>(() => renderer.Render(new MvpUiRepository().Sale with { Delivery = null }, kind: ReceiptKind.Delivery));
        var form = new ReceiptPrintSettingsModel { Paper = "Ticket 58 mm" }; Assert.Equal(58, form.Layout().WidthMm);
        form.Width = "mal"; Assert.Throws<ArgumentException>(() => form.Layout());
        Assert.Throws<ArgumentException>(() => new LabelPrintOptions(Copies: 201).Validate());
        Assert.Throws<ArgumentException>(() => new PrintLayout(20, 50).Validate(true));
    }
    [Fact] public async Task LabelsRequirePermissionFreshActiveCodeAndKeepBusinessDataUntouched()
    {
        var repo = new MvpUiRepository(); var session = new UserSession();
        var service = new BarcodeLabelService(repo, Labels(), session); var product = new ProductData { Id = 1, Name = "Producto", Sku = "SKU" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewAsync(product, 1, new()));
        session.Begin(new(1, "lector", "Lector", 1, [], ["INVENTARIO.VER"], false));
        repo.Barcodes.Add(new() { Id = 1, Code = "GD0000000001", Type = "CODE128", Active = false });
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(product, 1, new()));
        repo.Barcodes[0].Active = true; Assert.True((await service.PreviewAsync(product, 1, new())).IsLabel); Assert.Empty(repo.Writes);
    }
    [Fact] public async Task LocalPreferencesRoundTripWithoutChangingConnectionProfile()
    {
        var path = Path.Combine(Path.GetTempPath(), "GoDental-print-" + Guid.NewGuid(), "printing.json");
        try
        {
            var store = new LocalPrintPreferencesStore(path); Assert.Null((await store.LoadAsync()).Receipt);
            var value = new PrintPreferences(new(80, 250, 3), Labels: new(40, 30, Paper: "A4"));
            await store.SaveAsync(value); Assert.Equal(value, await store.LoadAsync()); Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
    private static void Dimensions(ReceiptDocument document, decimal width, decimal height)
    {
        Assert.Equal(width, document.WidthMm); Assert.Equal(height, document.HeightMm);
        // El motor PDF redondea MediaBox al cuarto de punto (≤0.045 mm).
        var match = Regex.Match(Encoding.Latin1.GetString(document.Pdf), @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");
        Assert.True(match.Success, "Falta MediaBox en el PDF");
        Assert.InRange(Math.Abs(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) - (double)width * 72 / 25.4), 0, .126);
        Assert.InRange(Math.Abs(double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) - (double)height * 72 / 25.4), 0, .126);
    }
    private static void Save(string name, ReceiptDocument document)
    {
        var dir = Environment.GetEnvironmentVariable("GODENTAL_PDF_PREVIEW_DIR"); if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, name + ".pdf"), document.Pdf);
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), document.Pages[0]);
    }
}
