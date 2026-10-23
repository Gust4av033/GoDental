using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface ICollectionRepository
{
    Task<MvpPage<CreditAccountData>> DueAccountsAsync(bool overdueOnly, MvpFilter filter, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> RegisterNoticeAsync(RegisterCollectionNotice request, int userId, int? workstationId, CancellationToken ct);
    Task<MvpPage<CollectionNoticeData>> NoticesAsync(long accountId, MvpFilter filter, int userId, CancellationToken ct);
    Task<CustomerStatement?> StatementAsync(long customerId, bool forPrint, int userId, CancellationToken ct);
    Task<PaymentReceiptData?> PaymentReceiptAsync(long paymentId, int userId, CancellationToken ct);
}
public interface ICreditDocumentRenderer
{
    ReceiptDocument RenderStatement(CustomerStatement statement, PrintLayout layout);
    ReceiptDocument RenderPayment(PaymentReceiptData receipt, PrintLayout layout);
}
