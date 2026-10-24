using System.Windows;
using System.Printing;
using System.Windows.Controls;
using System.Windows.Documents;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Wpf.Common;
using GoDental.Wpf.ViewModels.Operations;

namespace GoDental.Wpf.Navigation;

public sealed class PdfPrintService(ISessionController session) : IPdfPrintService
{
    public Task<bool> PrintAsync(ReceiptDocument document, string name)
    {
        var revision = session.Revision;
        void CheckSession()
        {
            if (session.Revision != revision || !session.IsAuthenticated || !(document.IsLabel ? session.Permissions.Contains("INVENTARIO.VER") : session.Permissions.Contains("VENTAS.VER") && session.Permissions.Contains("VENTAS.REIMPRIMIR")))
                throw new UnauthorizedAccessException("La sesión cambió antes de imprimir.");
        }
        CheckSession();
        var dialog = new PrintDialog { UserPageRangeEnabled = false };
        dialog.PrintTicket.PageMediaSize = new PageMediaSize((double)document.WidthMm * 96 / 25.4, (double)document.HeightMm * 96 / 25.4);
        dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
        dialog.PrintTicket.PageScalingFactor = 100;
        if (dialog.ShowDialog() != true) return Task.FromResult(false);
        CheckSession();
        dialog.PrintTicket.PageScalingFactor = 100;
        dialog.PrintTicket = dialog.PrintQueue.MergeAndValidatePrintTicket(dialog.PrintQueue.DefaultPrintTicket, dialog.PrintTicket).ValidatedPrintTicket;
        var capabilities = dialog.PrintQueue.GetPrintCapabilities(dialog.PrintTicket);
        var area = capabilities.PageImageableArea;
        var width = area?.ExtentWidth ?? dialog.PrintableAreaWidth;
        var height = area?.ExtentHeight ?? dialog.PrintableAreaHeight;
        var pages = BuildDocument(document,
            new(capabilities.OrientedPageMediaWidth ?? width, capabilities.OrientedPageMediaHeight ?? height),
            new(area?.OriginWidth ?? 0, area?.OriginHeight ?? 0, width, height));
        CheckSession();
        dialog.PrintDocument(pages.DocumentPaginator, name);
        return Task.FromResult(true);
    }
    public static FixedDocument BuildDocument(ReceiptDocument document, Size pageSize, Rect imageableArea)
    {
        if (document.Pages.Count == 0 || imageableArea.Width <= 0 || imageableArea.Height <= 0) throw new ArgumentException("No hay páginas o área imprimible disponible.");
        var width = (double)document.WidthMm * 96 / 25.4;
        var height = (double)document.HeightMm * 96 / 25.4;
        var margin = (double)document.MarginMm * 96 / 25.4;
        if (width <= 0 || height <= 0 || Math.Abs(pageSize.Width - width) > 4 || Math.Abs(pageSize.Height - height) > 4)
            throw new ArgumentException("El papel de la impresora no coincide con el documento. Selecciona esas medidas o regenera la vista previa para el papel disponible.");
        var left = (pageSize.Width - width) / 2; var top = (pageSize.Height - height) / 2;
        var safeArea = imageableArea; safeArea.Inflate(.5, .5);
        if (!safeArea.Contains(new Rect(left + margin, top + margin, width - 2 * margin, height - 2 * margin)))
            throw new ArgumentException("La impresora recorta el contenido con ese margen. Aumenta el margen y regenera la vista previa; las etiquetas necesitan un papel compatible.");
        var pages = new FixedDocument();
        foreach (var bytes in document.Pages)
        {
            var page = new FixedPage { Width = pageSize.Width, Height = pageSize.Height };
            var image = new Image { Source = ReceiptPageImage.Load(bytes, preview: false), Width = width, Height = height };
            FixedPage.SetLeft(image, left);
            FixedPage.SetTop(image, top);
            page.Children.Add(image); var content = new PageContent { Child = page }; pages.Pages.Add(content);
        }
        return pages;
    }
}
