using GoDental.Application.DTOs;
using GoDental.Domain.Rules;
namespace GoDental.Application.Validators;
public static class PurchaseValidator
{
    public static bool Cost(decimal value) => value >= 0 && value <= 999999999999.999999m && decimal.Round(value, 6) == value;
    public static bool Rate(decimal value) => value > 0 && value <= 9999999999.99999999m && decimal.Round(value, 8) == value;
    public static string? Validate(object request) => request switch
    {
        SavePurchaseOrder r => r.OperationId == Guid.Empty || r.Id < 0 || (r.Id > 0 && !MvpValidator.Version(r.ExpectedVersion)) || r.ExpectedVersion is null || r.SupplierId <= 0 || r.CurrencyId <= 0 || !Rate(r.EstimatedRate) || r.Date.TimeOfDay != TimeSpan.Zero || r.Date.Year < 1900 || !MvpValidator.Reason(r.Reason) || r.Lines is null || r.Lines.Count is < 1 or > 100
            || r.Lines.Any(l => l is null || l.ProductId <= 0 || !MvpValidator.Quantity(l.Quantity) || !Cost(l.UnitCost) || !MvpValidator.Money(l.Discount) || l.Discount > l.Quantity * l.UnitCost || !MvpValidator.Money(PurchaseRules.Money(l.Quantity * l.UnitCost)))
            || r.Lines.Select(l => l.ProductId).Distinct().Count() != r.Lines.Count ? "Revisa proveedor, moneda, tipo de cambio, cantidades, costos y descuentos. Usa una línea por producto y hasta 100 productos." : null,
        PurchaseTransition r => r.OperationId == Guid.Empty || r.OrderId <= 0 || !MvpValidator.Version(r.ExpectedVersion) || !MvpValidator.Reason(r.Reason) || r.State is not ("ENVIADA" or "CONFIRMADA" or "EN_PRODUCCION" or "DESPACHADA" or "CANCELADA") ? "Revisa estado, versión y motivo de la orden." : null,
        PreparePurchaseReceipt r => r.OperationId == Guid.Empty || r.OrderId <= 0 || !MvpValidator.Version(r.ExpectedOrderVersion) || r.WarehouseId <= 0 || !Rate(r.ExchangeRate) || !MvpValidator.Reason(r.Reason) || r.Lines is null || r.Lines.Count is < 1 or > 100
            || r.Lines.Any(l => l is null || l.OrderLineId <= 0 || !MvpValidator.Quantity(l.Quantity) || !Cost(l.ExtraUnitCostUsd) || l.LotNumber?.Length > 100 || l.Manufactured > l.Expires || l.Expires?.TimeOfDay > TimeSpan.Zero || l.Manufactured?.TimeOfDay > TimeSpan.Zero) ? "Revisa orden, almacén, cambio real, cantidades, costo adicional unitario y lotes." : null,
        PurchaseReceiptAction r => r.OperationId == Guid.Empty || r.ReceiptId <= 0 || !MvpValidator.Version(r.ExpectedVersion) || !MvpValidator.Version(r.ExpectedOrderVersion) || !MvpValidator.Reason(r.Reason) ? "Actualiza orden y recepción y registra un motivo." : null,
        _ => "Solicitud de compra desconocida."
    };
    public static SavePurchaseOrder Freeze(SavePurchaseOrder r) => r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim(), Lines = r.Lines.Select(l => l with { }).OrderBy(l => l.ProductId).ToArray() };
    public static PreparePurchaseReceipt Freeze(PreparePurchaseReceipt r) => r with { ExpectedOrderVersion = r.ExpectedOrderVersion.ToArray(), Reason = r.Reason.Trim(), Lines = r.Lines.Select(l => l with { LotNumber = l.LotNumber?.Trim() }).OrderBy(l => l.OrderLineId).ThenBy(l => l.LotNumber, StringComparer.Ordinal).ThenBy(l => l.Expires).ThenBy(l => l.Manufactured).ThenBy(l => l.Quantity).ThenBy(l => l.ExtraUnitCostUsd).ToArray() };
}
