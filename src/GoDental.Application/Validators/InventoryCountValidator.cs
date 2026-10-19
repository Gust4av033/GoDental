using GoDental.Application.DTOs;
using GoDental.Domain.Rules;

namespace GoDental.Application.Validators;

public static class InventoryCountValidator
{
    public static string? Validate(StartInventoryCount request)
    {
        if (request.WarehouseId <= 0) return "Selecciona un almacén.";
        if (request.ProductIds is null || request.ProductIds.Count is < 1 or > 500 || request.ProductIds.Any(id => id <= 0)
            || request.ProductIds.Distinct().Count() != request.ProductIds.Count)
            return "Selecciona entre 1 y 500 productos diferentes.";
        return ReasonError(request.Reason);
    }

    public static string? Validate(CaptureInventoryCount request)
    {
        if (VersionError(request.CountId, request.ExpectedVersion) is { } versionError) return versionError;
        if (request.Lines is null || request.Lines.Count is < 1 or > 1000
            || request.Lines.Any(line => line is null || line.LineId <= 0 || !InventoryCountRules.IsValidQuantity(line.Quantity))
            || request.Lines.Select(line => line.LineId).Distinct().Count() != request.Lines.Count)
            return "Captura entre 1 y 1000 líneas diferentes con cantidades no negativas de hasta cuatro decimales.";
        return null;
    }

    public static string? Validate(FinalizeInventoryCount request) =>
        VersionError(request.CountId, request.ExpectedVersion) ?? ReasonError(request.Reason);

    private static string? VersionError(long countId, byte[]? version) => countId <= 0 || version?.Length != 8
        ? "Recarga el conteo antes de guardar; se requiere su versión vigente." : null;

    private static string? ReasonError(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500
        ? "Escribe un motivo de hasta 500 caracteres." : null;
}
