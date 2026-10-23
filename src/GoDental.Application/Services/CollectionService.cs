using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class CollectionService(ICollectionRepository repository, ICreditDocumentRenderer renderer, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanRead => Has("CLIENTES.VER", "VENTAS.VER");
    public bool CanManage => CanRead && Has("CLIENTES.COBRANZA_GESTIONAR");
    public bool CanPrint => CanRead && Has("VENTAS.REIMPRIMIR");
    public Task<MvpPage<CreditAccountData>> DueAccountsAsync(bool overdueOnly, MvpFilter filter, CancellationToken ct = default)
    { Read("CLIENTES.VER", "VENTAS.VER"); Page(filter); return repository.DueAccountsAsync(overdueOnly, filter, UserId, ct); }
    public Task<Result<MvpReceipt>> RegisterAsync(RegisterCollectionNotice request, CancellationToken ct = default)
    {
        if (!CanManage) return Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN", "No tienes permiso para registrar avisos de cobranza."));
        if (CollectionValidator.Validate(request) is { } error) return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION", error));
        return repository.RegisterNoticeAsync(CollectionValidator.Freeze(request), UserId, WorkstationId, ct);
    }
    public Task<MvpPage<CollectionNoticeData>> NoticesAsync(long accountId, MvpFilter filter, CancellationToken ct = default)
    { Read("CLIENTES.VER", "VENTAS.VER"); Id(accountId); Page(filter); return repository.NoticesAsync(accountId, filter, UserId, ct); }
    public Task<CustomerStatement?> StatementAsync(long customerId, CancellationToken ct = default)
    { Read("CLIENTES.VER", "VENTAS.VER"); Id(customerId); return repository.StatementAsync(customerId, false, UserId, ct); }
    public Task<ReceiptDocument> PreviewStatementAsync(long customerId, PrintLayout layout, CancellationToken ct = default) => GenerateAsync(customerId, layout, async user =>
        renderer.RenderStatement(await repository.StatementAsync(customerId, true, user, ct) ?? throw new ArgumentException("El cliente no existe."), layout), ct);
    public Task<ReceiptDocument> PreviewPaymentAsync(long paymentId, PrintLayout layout, CancellationToken ct = default) => GenerateAsync(paymentId, layout, async user =>
        renderer.RenderPayment(await repository.PaymentReceiptAsync(paymentId, user, ct) ?? throw new ArgumentException("El abono no existe."), layout), ct);
    private async Task<ReceiptDocument> GenerateAsync(long id, PrintLayout layout, Func<int, Task<ReceiptDocument>> generate, CancellationToken ct)
    {
        Read("CLIENTES.VER", "VENTAS.VER", "VENTAS.REIMPRIMIR"); Id(id); layout.Validate(layout.WidthMm < 140);
        var revision = CurrentSession is ISessionController s ? s.Revision : 0;
        // IO del repositorio es asíncrona; la composición CPU queda fuera del hilo WPF.
        var user = UserId; var result = await Task.Run(() => generate(user), ct);
        Read("CLIENTES.VER", "VENTAS.VER", "VENTAS.REIMPRIMIR");
        if (CurrentSession is ISessionController current && current.Revision != revision) throw new UnauthorizedAccessException("La sesión cambió durante la generación.");
        return result;
    }
}
