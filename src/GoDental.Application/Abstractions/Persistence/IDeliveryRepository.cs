using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IDeliveryRepository
{
    Task<SaleQuote> QuoteDeliveryAsync(long orderId,int cashBoxId,int userId,CancellationToken ct);
    Task<DeliveryData?> DeliveryAsync(long orderId,int userId,CancellationToken ct);
    Task<Result<MvpReceipt>> DispatchDeliveryAsync(DispatchDelivery request,int userId,int? station,CancellationToken ct);
    Task<Result<MvpReceipt>> CompleteDeliveryAsync(CompleteDelivery request,int userId,int? station,CancellationToken ct);
    Task<Result<MvpReceipt>> SettleDeliveryAsync(SettleDelivery request,int userId,int? station,CancellationToken ct);
}
