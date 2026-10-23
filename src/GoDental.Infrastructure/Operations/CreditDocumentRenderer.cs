using System.Globalization;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace GoDental.Infrastructure.Operations;

public sealed class CreditDocumentRenderer(IOptions<ReceiptPdfOptions> options) : ICreditDocumentRenderer
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");
    private static string Money(decimal n) => n.ToString("$ #,##0.00", Usd);
    private static string Date(DateTimeOffset d) => d.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm", Usd);
    private static readonly Lazy<byte[]> Logo = new(() =>
    {
        using var s = typeof(CreditDocumentRenderer).Assembly.GetManifestResourceStream("GoDental.Branding.Logo.png")!;
        using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray();
    });
    public ReceiptDocument RenderPayment(PaymentReceiptData p, PrintLayout layout) => Render(layout, "RECIBO INTERNO DE ABONO", p.Number, Date(p.Date), c =>
    {
        c.Item().Text(p.Customer).Bold(); c.Item().Text($"Venta {p.Sale} · Cuenta {p.AccountId}");
        if (p.Voided) c.Item().Text("ABONO ANULADO · No acredita un pago vigente").Bold().FontColor(Colors.Red.Darken2);
        c.Item().Text("Importe original del abono: " + Money(p.Amount)).Bold();
        c.Item().Text("Método registrado: " + p.Method);
        if (!string.IsNullOrWhiteSpace(p.Reference)) c.Item().Text("Referencia: " + p.Reference);
        c.Item().Text("Registrado por: " + p.User);
        c.Item().Text("Saldo antes de este abono: " + (p.BalanceBefore is { } before ? Money(before) : "No disponible en el registro histórico"));
        c.Item().Text("Saldo después de este abono: " + (p.BalanceAfter is { } after ? Money(after) : "No disponible en el registro histórico"));
        c.Item().Text("Saldo al consultar (" + Date(p.AsOf) + "): " + Money(p.CurrentBalance));
        c.Item().Text("El saldo actual puede incluir abonos y devoluciones posteriores; no modifica este recibo.").FontSize(8);
        if (p.HistoricalDetailsMissing) c.Item().Text("Registro anterior a los snapshots: nombres de referencia actuales y saldos históricos no reconstruidos.").FontSize(8);
    });
    public ReceiptDocument RenderStatement(CustomerStatement s, PrintLayout layout) => Render(layout, "ESTADO DE CUENTA INTERNO", s.Code, Date(s.AsOf), c =>
    {
        c.Item().Text(s.Customer).Bold(); c.Item().Text("Cliente: " + s.Code);
        c.Item().Text("Saldo registrado: " + Money(s.RecordedBalance)).Bold();
        c.Item().Text("Saldo cobrable: " + Money(s.CollectibleBalance));
        if (s.Difference != 0) c.Item().Text($"REVISAR HISTORIAL: diferencia de {Money(s.Difference)} entre saldo registrado y movimientos disponibles. No se creó un ajuste.").Bold().FontColor(Colors.Red.Darken2);
        if (s.Accounts.Any(a => a.State == "CASTIGADA")) c.Item().Text("Incluye cuentas CASTIGADA separadas del saldo cobrable; no se ha ejecutado un castigo nuevo.").FontSize(8);
        c.Item().Text("MOVIMIENTOS DE CRÉDITO · USD").Bold();
        if (s.Movements.Count == 0) c.Item().Text("Sin movimientos de crédito registrados.");
        foreach (var m in s.Movements)
        {
            c.Item().BorderBottom(.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).Column(row =>
            {
                row.Item().Text($"{Date(m.Date)} · {m.Type}").SemiBold();
                row.Item().Text($"{m.Document} · {m.Sale} · Cuenta {m.AccountId}");
                row.Item().Text($"Cargo {Money(m.Charge)} · Reducción {Money(m.Reduction)}");
                row.Item().Text("Saldo acumulado: " + Money(m.RunningBalance));
                if (m.CashRefund > 0) row.Item().Text($"Reintegro de efectivo {Money(m.CashRefund)} (informativo; no reduce CxC otra vez).").FontSize(8);
            });
        }
        c.Item().Text("Saldo calculado: " + Money(s.CalculatedBalance)).Bold();
        c.Item().Text("CUENTAS INCLUIDAS").Bold();
        foreach (var a in s.Accounts)
            c.Item().Text($"Cuenta {a.Id} · {a.Sale} · {a.State} · vence {a.Due:dd/MM/yyyy} · saldo {Money(a.Balance)}");
        c.Item().Text("Los cargos son el importe financiado, sin sumar el efectivo inicial de ventas mixtas. Sólo posventa aplicada reduce deuda. Estado al momento de consulta; no es cierre histórico.").FontSize(8);
    });
    internal ReceiptDocument Render(PrintLayout layout, string title, string number, string date, Action<ColumnDescriptor> body)
    {
        layout.Validate(layout.WidthMm < 140);
        if (!Enum.TryParse<LicenseType>(options.Value.License, true, out var license) || !Enum.IsDefined(license)) throw new InvalidOperationException("Configura Pdf:License.");
        QuestPDF.Settings.License = license;
        try
        {
            var document = Document.Create(d => d.Page(page =>
            {
                var narrow = layout.WidthMm < 140;
                page.Size((float)layout.WidthMm, (float)layout.HeightMm, Unit.Millimetre); page.Margin((float)layout.MarginMm, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(narrow ? 8 : 10).FontColor("#18334D"));
                page.Header().PaddingBottom(7).Column(h =>
                {
                    if (!narrow) h.Item().Width(100).Image(Logo.Value);
                    h.Item().Text(options.Value.IssuerName).Bold().FontSize(narrow ? 12 : 16);
                    h.Item().Text(title).SemiBold(); h.Item().Text(number + " · " + date);
                });
                page.Content().Column(c => { c.Spacing(6); body(c); });
                page.Footer().PaddingTop(5).Column(f =>
                {
                    f.Item().Text("Documento interno · No es DTE · USD").FontSize(7);
                    f.Item().AlignRight().Text(t => { t.Span("Página "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                });
            }));
            var pdf = document.GeneratePdf();
            var pages = document.GenerateImages(new ImageGenerationSettings { RasterDpi = 144 }).Take(41).ToArray();
            if (pages.Length > 40) throw new ArgumentException("El documento supera 40 páginas de vista previa; aumenta las medidas del papel. No se generó un documento parcial.");
            return new(pdf, pages, layout.WidthMm, layout.HeightMm, layout.MarginMm);
        }
        catch (QuestPDF.Drawing.Exceptions.DocumentLayoutException) { throw new ArgumentException("El documento no cabe. Aumenta las medidas o reduce el margen y vuelve a generar."); }
    }
}
