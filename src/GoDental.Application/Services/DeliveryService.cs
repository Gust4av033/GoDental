using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class DeliveryService(IDeliveryRepository repository,IUserSession session):MvpServiceAccess(session)
{
    public bool CanRead=>Has("VENTAS.VER");
    public bool CanManage=>Has("VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR");
    public bool CanSettle=>Has("VENTAS.VER","CAJA.VER","CAJA.REPARTO_RENDIR");
    public Task<SaleQuote> QuoteAsync(long order,int box,CancellationToken ct=default){Read("VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR");Id(order);Id(box);return repository.QuoteDeliveryAsync(order,box,UserId,ct);}
    public Task<DeliveryData?> ReadAsync(long order,CancellationToken ct=default){Read("VENTAS.VER");Id(order);return repository.DeliveryAsync(order,UserId,ct);}
    public Task<Result<MvpReceipt>> DispatchAsync(DispatchDelivery r,CancellationToken ct=default)=>Send(CanManage,DeliveryValidator.Validate(r),()=>repository.DispatchDeliveryAsync(DeliveryValidator.Freeze(r),UserId,WorkstationId,ct));
    public Task<Result<MvpReceipt>> CompleteAsync(CompleteDelivery r,CancellationToken ct=default)=>Send(CanManage,DeliveryValidator.Validate(r),()=>repository.CompleteDeliveryAsync(DeliveryValidator.Freeze(r),UserId,WorkstationId,ct));
    public Task<Result<MvpReceipt>> SettleAsync(SettleDelivery r,CancellationToken ct=default)=>Send(CanSettle,DeliveryValidator.Validate(r),()=>repository.SettleDeliveryAsync(DeliveryValidator.Freeze(r),UserId,WorkstationId,ct));
    private static Task<Result<MvpReceipt>> Send(bool allowed,string? error,Func<Task<Result<MvpReceipt>>> write)=>!allowed?Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso para esta operación de reparto.")):error is not null?Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error)):write();
}
