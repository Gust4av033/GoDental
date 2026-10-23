using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;
public static class DeliveryValidator
{
    private static bool Base(Guid id,long order,byte[] version,string reason) => id!=Guid.Empty && order>0 && MvpValidator.Version(version) && MvpValidator.Reason(reason);
    public static string? Validate(DispatchDelivery r) => !Base(r.OperationId,r.OrderId,r.ExpectedVersion,r.Reason)||r.CashBoxId<=0||string.IsNullOrWhiteSpace(r.QuoteToken)||string.IsNullOrWhiteSpace(r.Courier)||r.Courier.Trim().Length>150 ? "Revisa pedido, caja, precio, responsable y motivo." : null;
    public static string? Validate(CompleteDelivery r) => !Base(r.OperationId,r.OrderId,r.ExpectedVersion,r.Reason)||r.Outcome is not("ENTREGADO" or "INCIDENCIA" or "RETORNADO")||r.Reference?.Length>150
        ||(r.Outcome=="ENTREGADO" ? r.Medium is not("EFECTIVO" or "TRANSFERENCIA")||r.Amount<=0||!MvpValidator.Money(r.Amount)||r.Medium=="TRANSFERENCIA"&&string.IsNullOrWhiteSpace(r.Reference) : r.Amount!=0||r.Medium is not null)
        ||r.Outcome=="RETORNADO"&&!r.AllRecovered ? "Revisa resultado, pago completo o recuperación completa sellada de la mercadería." : null;
    public static string? Validate(SettleDelivery r) => !Base(r.OperationId,r.OrderId,r.ExpectedVersion,r.Reason)||r.Amount<=0||!MvpValidator.Money(r.Amount)||r.ShiftId is <=0||r.Reference?.Length>150 ? "Revisa monto exacto, turno y referencia de la rendición." : null;
    public static DispatchDelivery Freeze(DispatchDelivery r)=>r with{ExpectedVersion=r.ExpectedVersion.ToArray(),Courier=r.Courier.Trim(),Reason=r.Reason.Trim()};
    public static CompleteDelivery Freeze(CompleteDelivery r)=>r with{ExpectedVersion=r.ExpectedVersion.ToArray(),Reference=r.Reference?.Trim(),Reason=r.Reason.Trim()};
    public static SettleDelivery Freeze(SettleDelivery r)=>r with{ExpectedVersion=r.ExpectedVersion.ToArray(),Reference=r.Reference?.Trim(),Reason=r.Reason.Trim()};
}
