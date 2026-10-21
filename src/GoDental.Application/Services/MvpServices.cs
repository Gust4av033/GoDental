using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Application.Services;
public abstract class MvpServiceAccess(IUserSession session)
{
    protected IUserSession CurrentSession => session;
    protected int UserId => checked((int)session.UserId!.Value);
    protected int? WorkstationId => checked((int?)session.WorkstationId);

    protected bool Has(params string[] codes) => session.IsAuthenticated && codes.All(session.Permissions.Contains);
    protected void Read(params string[] codes)
    {
        if (!Has(codes))
            throw new UnauthorizedAccessException("No tienes permiso para esta consulta.");
    }

    protected static void Id(long id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));
    }

    protected static void Page(MvpFilter f)
    {
        if (!MvpValidator.Page(f))
            throw new ArgumentException("Revisa la página de la consulta.");
    }

    protected static Task<Result<MvpReceipt>> Write(bool allowed, object r, Func<Task<Result<MvpReceipt>>> save)
    {
        if (!allowed)
            return Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN", "No tienes permiso para esta operación."));
        return MvpValidator.Validate(r) is { } error ? Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION", error)) : save();
    }
}

public sealed class SalesService(ISalesRepository repository, IUserSession session, IIdentityGateway identity) : MvpServiceAccess(session)
{
    public bool CanRead => Has("VENTAS.VER");
    public bool CanSell => Has("VENTAS.VER", "VENTAS.CREAR");
    public bool CanQuote => Has("VENTAS.VER", "VENTAS.COTIZAR");

    public Task<IReadOnlyList<PaymentMethodData>> PaymentMethodsAsync(CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        return repository.PaymentMethodsAsync(UserId, ct);
    }

    public Task<MvpPage<PosProductData>> SearchProductsAsync(int boxId, string search, MvpFilter filter, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Id(boxId);
        Page(filter);
        if (search is null || search.Length > 200)
            throw new ArgumentException("Revisa la búsqueda.");
        return repository.SearchProductsAsync(boxId, search.Trim(), filter, UserId, ct);
    }

    public Task<SaleQuote> QuoteAsync(SaleCart cart, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        if (MvpValidator.Cart(cart) is { } error)
            throw new ArgumentException(error);
        return repository.QuoteAsync(MvpValidator.Freeze(cart), UserId, ct);
    }

    public Task<Result<MvpReceipt>> ConfirmAsync(ConfirmSale r, CancellationToken ct = default) => Write(CanSell, r, () => repository.ConfirmAsync(r with { Cart = MvpValidator.Freeze(r.Cart), Payments = r.Payments.Select(p => p with { Reference = p.Reference?.Trim() }).ToArray(), Reason = r.Reason.Trim(), Order = r.Order is { } o ? o with { ExpectedVersion = o.ExpectedVersion.ToArray() } : null }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> SaveQuotationAsync(SaveQuotation r, CancellationToken ct = default) => Write(CanQuote, r, () => repository.SaveQuotationAsync(r with { Cart = MvpValidator.Freeze(r.Cart), Reason = r.Reason.Trim() }, UserId, WorkstationId, ct));
    public async Task<Result<MvpReceipt>> AuthorizeDiscountAsync(DiscountApproval r, string administrator, string password, CancellationToken ct = default)
    {
        if (!CanSell)
            return Result<MvpReceipt>.Failure("FORBIDDEN", "No tienes permiso para solicitar esta autorización.");
        if (MvpValidator.Validate(r) is { } error)
            return Result<MvpReceipt>.Failure("VALIDATION", error);
        var revision = CurrentSession is ISessionController s ? s.Revision : 0;
        var verified = await identity.SignInAsync(administrator, password, Environment.MachineName, ct);
        if (!verified.IsSuccess || verified.Value is null || verified.Value.MustChangePassword || !verified.Value.Roles.Contains("Administrador") || !verified.Value.Permissions.Contains("VENTAS.DESCUENTO_MAYOR_35"))
            return Result<MvpReceipt>.Failure("FORBIDDEN", "No se pudo autorizar con esas credenciales administrativas.");
        if (CurrentSession is ISessionController current && revision != current.Revision)
            return Result<MvpReceipt>.Failure("FORBIDDEN", "La sesión cambió durante la autorización.");
        return await repository.AuthorizeDiscountAsync(r with { Cart = MvpValidator.Freeze(r.Cart), Reason = r.Reason.Trim() }, verified.Value.Id, UserId, WorkstationId, ct);
    }

    public Task<SaleSnapshot?> ReadAsync(long id, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Id(id);
        return repository.ReadSaleAsync(id, UserId, ct);
    }

    public Task<QuotationData?> ReadQuotationAsync(long id, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Id(id);
        return repository.ReadQuotationAsync(id, UserId, ct);
    }

    public Task<MvpPage<QuotationData>> QuotationsAsync(MvpFilter f, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Page(f);
        return repository.QuotationsAsync(f, UserId, ct);
    }

    public Task<MvpPage<SaleData>> SearchAsync(MvpFilter f, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Page(f);
        return repository.SalesAsync(f, UserId, ct);
    }
}

public sealed class CashService(ICashRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanRead => Has("CAJA.VER");
    public bool CanOpen => Has("CAJA.VER", "CAJA.ABRIR");
    public bool CanClose => Has("CAJA.VER", "CAJA.CERRAR");
    public bool CanMove => Has("CAJA.VER", "CAJA.MOVIMIENTO");

    public Task<IReadOnlyList<CashBoxData>> BoxesAsync(CancellationToken ct = default)
    {
        Read("CAJA.VER");
        return repository.CashBoxesAsync(UserId, ct);
    }

    public Task<CashShiftData?> CurrentAsync(int boxId, CancellationToken ct = default)
    {
        Read("CAJA.VER");
        Id(boxId);
        return repository.ReadShiftAsync(boxId, UserId, ct);
    }

    public Task<CashShiftData?> ReadAsync(long id, CancellationToken ct = default)
    {
        Read("CAJA.VER");
        Id(id);
        return repository.ReadShiftByIdAsync(id, UserId, ct);
    }

    public Task<MvpPage<CashMovementData>> MovementsAsync(long id, MvpFilter f, CancellationToken ct = default)
    {
        Read("CAJA.VER");
        Id(id);
        Page(f);
        return repository.CashMovementsAsync(id, f, UserId, ct);
    }

    public Task<Result<MvpReceipt>> SaveBoxAsync(CashBoxChange r, CancellationToken ct = default) => Write(Has("CAJA.VER", "AJUSTES.EDITAR"), r, () => repository.SaveBoxAsync(r with { Code=r.Code?.Trim() ?? "", Name=r.Name.Trim(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> OpenAsync(OpenCashShift r, CancellationToken ct = default) => Write(CanOpen, r, () => repository.OpenShiftAsync(r with { Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> MoveAsync(CashMovement r, CancellationToken ct = default) => Write(CanMove, r, () => repository.MoveCashAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> CloseAsync(CloseCashShift r, CancellationToken ct = default) => Write(CanClose, r, () => repository.CloseShiftAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<MvpPage<CreditAccountData>> AccountsAsync(bool overdueOnly, MvpFilter f, CancellationToken ct = default)
    {
        Read("CLIENTES.VER", "VENTAS.VER");
        Page(f);
        return repository.AccountsAsync(overdueOnly, f, UserId, ct);
    }

    public Task<Result<MvpReceipt>> PayAsync(AccountPayment r, CancellationToken ct = default) => Write(Has("CAJA.VER", "CAJA.MOVIMIENTO", "CLIENTES.VER", "VENTAS.VER"), r, () => repository.PayAccountAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim(), Reference=r.Reference?.Trim() }, UserId, WorkstationId, ct));
}

public sealed class OrderService(IOrderRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanRead => Has("VENTAS.VER");
    public bool CanManage => Has("VENTAS.VER", "VENTAS.CREAR");

    public Task<Result<MvpReceipt>> PlaceAsync(PlaceOrder r, CancellationToken ct = default) => Write(CanManage, r, () => repository.PlaceOrderAsync(r with { Items = r.Items.Select(i => i with { }).OrderBy(i=>i.ProductId).ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> TransitionAsync(OrderTransition r, CancellationToken ct = default) => Write(CanManage, r, () => repository.TransitionOrderAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> LinkSaleAsync(LinkOrderSale r, CancellationToken ct = default) => Write(CanManage, r, () => repository.LinkSaleAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<OrderSnapshot?> ReadAsync(long id, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Id(id);
        return repository.ReadOrderAsync(id, UserId, ct);
    }

    public Task<MvpPage<OrderEventData>> EventsAsync(long id, MvpFilter f, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Id(id);
        Page(f);
        return repository.OrderEventsAsync(id, f, UserId, ct);
    }

    public Task<MvpPage<OrderData>> SearchAsync(string? state, MvpFilter f, CancellationToken ct = default)
    {
        Read("VENTAS.VER");
        Page(f);
        if (state is not (null or "RECIBIDO" or "PREPARANDO" or "LISTO" or "EN_RUTA" or "ENTREGADO" or "INCIDENCIA" or "CANCELADO"))
            throw new ArgumentException("Estado desconocido.");
        return repository.OrdersAsync(state, f, UserId, ct);
    }
}

public sealed class InventorySetupService(IInventorySetupRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public Task<Result<MvpReceipt>> InitializeAsync(InitialInventory r, CancellationToken ct = default) => Write(Has("INVENTARIO.VER", "INVENTARIO.AJUSTAR"), r, () => repository.InitialInventoryAsync(r with { Reason=r.Reason.Trim(), LotNumber=r.LotNumber?.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> SaveBarcodeAsync(BarcodeChange r, CancellationToken ct = default) => Write(Has("INVENTARIO.VER", "INVENTARIO.PRODUCTOS_GESTIONAR"), r, () => repository.SaveBarcodeAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
}

public sealed class FiscalPendingService(IFiscalPendingRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public Task<Result<MvpReceipt>> RegisterAsync(PendingFiscal r, CancellationToken ct = default) => Write(Has("FISCAL.VER", "FISCAL.CONFIGURAR", "VENTAS.VER"), r, () => repository.RegisterPendingAsync(r with { Reason=r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<MvpPage<PendingFiscalData>> SearchAsync(MvpFilter f, CancellationToken ct = default)
    {
        Read("FISCAL.VER");
        Page(f);
        return repository.PendingAsync(f, UserId, ct);
    }
}
