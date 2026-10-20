using System.Windows;
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
            if (session.Revision != revision || !session.IsAuthenticated || !session.Permissions.Contains("VENTAS.VER") || !session.Permissions.Contains("VENTAS.REIMPRIMIR"))
                throw new UnauthorizedAccessException("La sesión cambió antes de imprimir.");
        }
        CheckSession();
        var dialog = new PrintDialog { UserPageRangeEnabled = false };
        if (dialog.ShowDialog() != true) return Task.FromResult(false);
        CheckSession();
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
        var scale = Math.Min(imageableArea.Width / 816, imageableArea.Height / 1056);
        var pages = new FixedDocument();
        foreach (var bytes in document.Pages)
        {
            var page = new FixedPage { Width = pageSize.Width, Height = pageSize.Height };
            var image = new Image { Source = ReceiptPageImage.Load(bytes, preview: false), Width = 816 * scale, Height = 1056 * scale };
            FixedPage.SetLeft(image, imageableArea.Left + (imageableArea.Width - image.Width) / 2);
            FixedPage.SetTop(image, imageableArea.Top + (imageableArea.Height - image.Height) / 2);
            page.Children.Add(image); var content = new PageContent { Child = page }; pages.Pages.Add(content);
        }
        return pages;
    }
}
