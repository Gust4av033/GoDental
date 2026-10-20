using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;

namespace GoDental.Application.Services;
public sealed class SaleReceiptService(ISalesRepository repository, IReceiptRenderer renderer, IUserSession session) : MvpServiceAccess(session)
{
    public async Task<byte[]> ExportAsync(long saleId, CancellationToken ct = default)
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
        var pdf = await Task.Run(() => renderer.Render(snapshot), ct);
        CheckSession();
        return pdf;
    }
}
