using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IPurchaseRepository
{
    Task<PurchaseCatalogs> PurchaseCatalogsAsync(int userId, CancellationToken ct);
    Task<MvpPage<PurchaseOrderData>> PurchaseOrdersAsync(string search, string? state, MvpFilter filter, int userId, CancellationToken ct);
    Task<MvpPage<ProductData>> PurchaseProductsAsync(string search, MvpFilter filter, int userId, CancellationToken ct);
    Task<PurchaseOrderSnapshot?> ReadPurchaseAsync(long id, int userId, CancellationToken ct);
    Task<PurchaseReceiptSnapshot?> ReadPurchaseReceiptAsync(long id, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> SavePurchaseAsync(SavePurchaseOrder request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> TransitionPurchaseAsync(PurchaseTransition request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> PrepareReceiptAsync(PreparePurchaseReceipt request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> ActOnReceiptAsync(PurchaseReceiptAction request, int userId, int? workstationId, CancellationToken ct);
}
