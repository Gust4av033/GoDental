using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class PurchaseService(IPurchaseRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanRead => Has("COMPRAS.VER");
    public bool CanManage => Has("COMPRAS.VER", "COMPRAS.GESTIONAR");
    public Task<PurchaseCatalogs> CatalogsAsync(CancellationToken ct = default) { Read("COMPRAS.VER"); return repository.PurchaseCatalogsAsync(UserId, ct); }
    public Task<MvpPage<PurchaseOrderData>> SearchAsync(string search, string? state, MvpFilter filter, CancellationToken ct = default)
    {
        Read("COMPRAS.VER"); Page(filter);
        if (search is null || search.Length > 200 || state is not (null or "BORRADOR" or "ENVIADA" or "CONFIRMADA" or "EN_PRODUCCION" or "DESPACHADA" or "RECIBIDA_PARCIAL" or "RECIBIDA" or "CANCELADA")) throw new ArgumentException("Revisa búsqueda y estado.");
        return repository.PurchaseOrdersAsync(search.Trim(), state, filter, UserId, ct);
    }
    public Task<MvpPage<ProductData>> ProductsAsync(string search, MvpFilter filter, CancellationToken ct = default)
    {
        Read("COMPRAS.VER"); Page(filter);
        if (search is null || search.Length > 200) throw new ArgumentException("Revisa la búsqueda de productos.");
        return repository.PurchaseProductsAsync(search.Trim(), filter, UserId, ct);
    }
    public Task<PurchaseOrderSnapshot?> ReadAsync(long id, CancellationToken ct = default) { Read("COMPRAS.VER"); Id(id); return repository.ReadPurchaseAsync(id, UserId, ct); }
    public Task<PurchaseReceiptSnapshot?> ReceiptAsync(long id, CancellationToken ct = default) { Read("COMPRAS.VER"); Id(id); return repository.ReadPurchaseReceiptAsync(id, UserId, ct); }
    public Task<Result<MvpReceipt>> SaveAsync(SavePurchaseOrder r, CancellationToken ct = default) => Save(r, () => repository.SavePurchaseAsync(PurchaseValidator.Freeze(r), UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> TransitionAsync(PurchaseTransition r, CancellationToken ct = default) => Save(r, () => repository.TransitionPurchaseAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() }, UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> PrepareAsync(PreparePurchaseReceipt r, CancellationToken ct = default) => Save(r, () => repository.PrepareReceiptAsync(PurchaseValidator.Freeze(r), UserId, WorkstationId, ct));
    public Task<Result<MvpReceipt>> ApplyAsync(PurchaseReceiptAction r, CancellationToken ct = default) => Save(r, () => repository.ActOnReceiptAsync(r with { ExpectedVersion = r.ExpectedVersion.ToArray(), ExpectedOrderVersion = r.ExpectedOrderVersion.ToArray(), Reason = r.Reason.Trim() }, UserId, WorkstationId, ct));
    private Task<Result<MvpReceipt>> Save(object r, Func<Task<Result<MvpReceipt>>> save) => !CanManage ? Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN", "No tienes permiso para gestionar compras."))
        : PurchaseValidator.Validate(r) is { } error ? Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION", error)) : save();
}
