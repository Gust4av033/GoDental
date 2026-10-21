using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;

namespace GoDental.Application.Services;
public sealed class SaleReceiptService(ISalesRepository repository, IReceiptRenderer renderer, IUserSession session) : MvpServiceAccess(session)
{
    public Task<byte[]> ExportAsync(long saleId, CancellationToken ct = default) => GenerateAsync(saleId, sale => renderer.Render(sale), ct);
    public Task<ReceiptDocument> PreviewAsync(long saleId, CancellationToken ct = default) => GenerateAsync(saleId, sale => renderer.RenderPreview(sale), ct);
    public Task<ReceiptDocument> PreviewAsync(long saleId, PrintLayout layout, ReceiptKind kind, CancellationToken ct = default)
        => GenerateAsync(saleId, sale => renderer.RenderPreview(sale, layout, kind), ct);
    private async Task<T> GenerateAsync<T>(long saleId, Func<SaleSnapshot, T> render, CancellationToken ct)
    {
        Read("VENTAS.VER", "VENTAS.REIMPRIMIR");
        Id(saleId);
        var revision = CurrentSession is ISessionController start ? start.Revision : 0;
        var snapshot = await repository.ReadReceiptAsync(saleId, UserId, ct);
        if (snapshot is null || snapshot.Sale.State != "CONFIRMADA")
            throw new ArgumentException("El comprobante requiere una venta confirmada.");
        void CheckSession()
        {
            if (!Has("VENTAS.VER", "VENTAS.REIMPRIMIR") || CurrentSession is ISessionController current && current.Revision != revision)
                throw new UnauthorizedAccessException("La sesión cambió durante la generación del comprobante.");
        }

        CheckSession();
        var pdf = await Task.Run(() => render(snapshot), ct);
        CheckSession();
        return pdf;
    }
}
