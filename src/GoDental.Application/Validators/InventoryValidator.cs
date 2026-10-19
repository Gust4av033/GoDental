using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;

public static class InventoryValidator
{
    public static string? Validate(InventoryAdjustment r)
    {
        if (r.ProductId <= 0 || r.WarehouseId <= 0) return "Selecciona producto y almacén.";
        if (r.Quantity == 0 || Math.Abs(r.Quantity) > 99999999999999.9999m || decimal.Round(r.Quantity, 4) != r.Quantity)
            return "La cantidad debe ser distinta de cero y tener como máximo cuatro decimales.";
        if (r.Type is not ("AJUSTE" or "DAÑO" or "VENCIMIENTO" or "TRANSFERENCIA")) return "Tipo de movimiento no permitido.";
        if (r.Type != "AJUSTE" && r.Quantity >= 0) return "Las bajas y transferencias requieren una cantidad de salida negativa.";
        if (r.Quantity > 0 && r.UnitCost is null) return "Indica el costo unitario real de la entrada.";
        if (r.UnitCost is { } cost && (cost < 0 || cost > 999999999999.999999m || decimal.Round(cost, 6) != cost)) return "Revisa el costo: admite hasta seis decimales.";
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Trim().Length > 500) return "Escribe un motivo de hasta 500 caracteres.";
        if (r.ExpectedVersion is null || r.ExpectedVersion.Length is not (0 or 8)) return "Recarga las existencias antes de confirmar.";
        if (r.LotId is <= 0) return "Selecciona un lote válido.";
        if (!string.IsNullOrWhiteSpace(r.NewLotNumber) && (r.LotId is not null || r.Quantity < 0 || r.NewLotNumber.Trim().Length > 100)) return "Un lote nuevo sólo corresponde a una entrada y no debe exceder 100 caracteres.";
        if (r.Manufactured > r.Expires) return "La fabricación no puede ser posterior al vencimiento.";
        if (r.Type == "TRANSFERENCIA" && (r.DestinationWarehouseId is null or <= 0 || r.DestinationWarehouseId == r.WarehouseId)) return "Selecciona un almacén destino diferente.";
        if (r.Type != "TRANSFERENCIA" && r.DestinationWarehouseId is not null) return "Sólo las transferencias admiten destino.";
        return null;
    }
}
