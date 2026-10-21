using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
namespace GoDental.Application.Services;

public sealed class BarcodeLabelService(IInventorySetupRepository repository, IBarcodeLabelRenderer renderer, IUserSession session) : MvpServiceAccess(session)
{
    public async Task<ReceiptDocument> PreviewAsync(ProductData product, long barcodeId, LabelPrintOptions options, CancellationToken ct = default)
    {
        Read("INVENTARIO.VER"); Id(product.Id); options.Validate();
        var revision = CurrentSession is ISessionController start ? start.Revision : 0;
        void Check()
        {
            if (!Has("INVENTARIO.VER") || CurrentSession is ISessionController current && current.Revision != revision)
                throw new UnauthorizedAccessException("La sesión cambió durante la generación de etiquetas.");
        }
        var barcodes = await repository.ReadBarcodesAsync(product.Id, UserId, ct);
        Check();
        var code = barcodes.Items.SingleOrDefault(x => x.Id == barcodeId && x.Active)
            ?? throw new ArgumentException("Selecciona un código activo y actualiza la ficha si cambió.");
        var content = new LabelContent(product.Name, product.Sku, code.Code, code.Type);
        var document = await Task.Run(() => renderer.RenderPreview(content, options), ct);
        Check(); return document;
    }
}
