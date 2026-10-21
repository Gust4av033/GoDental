using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;

public interface ISalesRepository
{
    Task<IReadOnlyList<PaymentMethodData>> PaymentMethodsAsync(int userId, CancellationToken ct);
    Task<MvpPage<PosProductData>> SearchProductsAsync(int boxId, string search, MvpFilter filter, int userId, CancellationToken ct);
    Task<SaleQuote> QuoteAsync(SaleCart cart, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> ConfirmAsync(ConfirmSale request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> SaveQuotationAsync(SaveQuotation request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> AuthorizeDiscountAsync(DiscountApproval request, int administratorId, int userId, int? workstationId, CancellationToken ct);
    Task<QuotationData?> ReadQuotationAsync(long id, int userId, CancellationToken ct);
    Task<MvpPage<QuotationData>> QuotationsAsync(MvpFilter filter, int userId, CancellationToken ct);
    Task<SaleSnapshot?> ReadReceiptAsync(long id, int userId, CancellationToken ct);
    Task<SaleSnapshot?> ReadSaleAsync(long id, int userId, CancellationToken ct);
    Task<MvpPage<SaleData>> SalesAsync(MvpFilter filter, int userId, CancellationToken ct);
}
public interface ICashRepository
{
    Task<IReadOnlyList<CashBoxData>> CashBoxesAsync(int userId, CancellationToken ct);
    Task<CashShiftData?> ReadShiftAsync(int boxId, int userId, CancellationToken ct);
    Task<CashShiftData?> ReadShiftByIdAsync(long id, int userId, CancellationToken ct);
    Task<MvpPage<CashMovementData>> CashMovementsAsync(long shiftId, MvpFilter filter, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> SaveBoxAsync(CashBoxChange request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> OpenShiftAsync(OpenCashShift request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> MoveCashAsync(CashMovement request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> CloseShiftAsync(CloseCashShift request, int userId, int? workstationId, CancellationToken ct);
    Task<MvpPage<CreditAccountData>> AccountsAsync(bool overdueOnly, MvpFilter filter, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> PayAccountAsync(AccountPayment request, int userId, int? workstationId, CancellationToken ct);
}
public interface IOrderRepository
{
    Task<Result<MvpReceipt>> PlaceOrderAsync(PlaceOrder request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> TransitionOrderAsync(OrderTransition request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> LinkSaleAsync(LinkOrderSale request, int userId, int? workstationId, CancellationToken ct);
    Task<OrderSnapshot?> ReadOrderAsync(long id, int userId, CancellationToken ct);
    Task<MvpPage<OrderData>> OrdersAsync(string? state, MvpFilter filter, int userId, CancellationToken ct);
    Task<MvpPage<OrderEventData>> OrderEventsAsync(long id, MvpFilter filter, int userId, CancellationToken ct);
}
public interface IInventorySetupRepository
{
    Task<Result<MvpReceipt>> InitialInventoryAsync(InitialInventory request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> SaveBarcodeAsync(BarcodeChange request, int userId, int? workstationId, CancellationToken ct);
}
public interface IFiscalPendingRepository
{
    Task<Result<MvpReceipt>> RegisterPendingAsync(PendingFiscal request, int userId, int? workstationId, CancellationToken ct);
    Task<MvpPage<PendingFiscalData>> PendingAsync(MvpFilter filter, int userId, CancellationToken ct);
}
public interface IMvpOperationRepository
{
    Task<MvpReceipt?> FindOperationAsync(Guid id, int userId, CancellationToken ct);
    Task<OperationResolution> ResolveOperationAsync(OperationTicket ticket, bool rejectIfAbsent, int userId, int? workstationId, CancellationToken ct);
}
