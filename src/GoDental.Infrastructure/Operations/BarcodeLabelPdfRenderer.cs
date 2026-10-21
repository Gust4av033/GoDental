using System.Globalization;
using System.Text;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using ZXing;
using ZXing.Common;

namespace GoDental.Infrastructure.Operations;

public sealed class BarcodeLabelPdfRenderer(IOptions<ReceiptPdfOptions> options) : IBarcodeLabelRenderer
{
    public ReceiptDocument RenderPreview(LabelContent content, LabelPrintOptions settings)
    {
        settings.Validate();
        if (content.Type is not ("CODE128" or "EAN13" or "UPC"))
            throw new ArgumentException("El tipo OTRO no define una simbología imprimible. Registra un código CODE128, EAN13 o UPC.");
        if (string.IsNullOrWhiteSpace(content.Code) || content.Code != content.Code.Trim() || BarcodeValidator.Validate(content.Code, content.Type) is not null)
            throw new ArgumentException("El código no es válido para su simbología.");
        if (!Enum.TryParse<LicenseType>(options.Value.License, true, out var license) || !Enum.IsDefined(license))
            throw new InvalidOperationException("Configura Pdf:License para generar etiquetas.");
        QuestPDF.Settings.License = license;
        var format = content.Type switch { "EAN13" => BarcodeFormat.EAN_13, "UPC" => BarcodeFormat.UPC_A, _ => BarcodeFormat.CODE_128 };
        var matrix = new MultiFormatWriter().encode(content.Code, format, 0, 1, new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 });
        const int quiet = 11;
        var modules = matrix.Width + 2 * quiet;
        var moduleMm = content.Type == "CODE128" ? .254m : .33m;
        var required = modules * moduleMm + 3;
        if (settings.WidthMm < required)
            throw new ArgumentException($"El código necesita al menos {Math.Ceiling(required)} mm de ancho con sus zonas libres. Aumenta el ancho de etiqueta.");
        var svg = new StringBuilder(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{settings.WidthMm - 3}\" height=\"{settings.HeightMm - 18}\" preserveAspectRatio=\"none\" viewBox=\"0 0 {modules} 100\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/>"));
        for (var x = 0; x < matrix.Width; x++)
            if (matrix[x, 0]) svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x + quiet}\" y=\"0\" width=\"1\" height=\"100\" fill=\"black\"/>");
        svg.Append("</svg>");
        var sheet = settings.Paper != "Etiqueta";
        var width = sheet ? settings.Paper == "A4" ? 210m : 215.9m : settings.WidthMm;
        var height = sheet ? settings.Paper == "A4" ? 297m : 279.4m : settings.HeightMm;
        var margin = sheet ? settings.MarginMm : 0m;
        var columns = sheet ? (int)((width - 2 * margin + settings.GapMm) / (settings.WidthMm + settings.GapMm)) : 1;
        var rows = sheet ? (int)((height - 2 * margin + settings.GapMm) / (settings.HeightMm + settings.GapMm)) : 1;
        if (columns < 1 || rows < 1) throw new ArgumentException("La etiqueta no cabe en el papel con esos márgenes.");
        var perPage = columns * rows;
        var pageCount = (settings.Copies + perPage - 1) / perPage;
        if (width * height * pageCount * 300 * 300 / (25.4m * 25.4m) > 120_000_000)
            throw new ArgumentException("Reduce las copias por solicitud para generar la vista previa de este tamaño.");
        var document = Document.Create(d =>
        {
            for (var first = 0; first < settings.Copies; first += perPage)
            {
                var count = Math.Min(perPage, settings.Copies - first);
                d.Page(page =>
                {
                    page.Size((float)width, (float)height, Unit.Millimetre); page.Margin((float)margin, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(7).FontColor("#000000"));
                    page.Content().Column(c =>
                    {
                        c.Spacing((float)settings.GapMm, Unit.Millimetre);
                        for (var row = 0; row < (count + columns - 1) / columns; row++)
                        {
                            var rowCount = Math.Min(columns, count - row * columns);
                            c.Item().Row(r =>
                            {
                                r.Spacing((float)settings.GapMm, Unit.Millimetre);
                                for (var col = 0; col < rowCount; col++)
                                    r.ConstantItem((float)settings.WidthMm, Unit.Millimetre).Height((float)settings.HeightMm, Unit.Millimetre)
                                        .Background("#FFFFFF").Padding(1.5f, Unit.Millimetre).Column(label =>
                                        {
                                            label.Item().Height(6, Unit.Millimetre).Text(content.ProductName).SemiBold().ClampLines(2);
                                            label.Item().Height(3, Unit.Millimetre).Text(content.Sku).FontSize(6).ClampLines(1);
                                            // Ancho y alto vectoriales independientes: nunca se recortan módulos ni quiet zones.
                                            label.Item().Height((float)(settings.HeightMm - 18), Unit.Millimetre).Svg(svg.ToString()).FitArea();
                                            label.Item().PaddingTop(1).AlignCenter().Text(content.Code).FontSize(7);
                                        });
                            });
                        }
                    });
                });
            }
        });
        return new(document.GeneratePdf(), document.GenerateImages(new ImageGenerationSettings { RasterDpi = 300 }).ToArray(),
            width, height, sheet ? margin : 1.5m, IsLabel: true);
    }
}
