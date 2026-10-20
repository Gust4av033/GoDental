using GoDental.Application.DTOs;

namespace GoDental.Application.Validators;

public static class ProductValidator
{
    public static string? Validate(ProductData p)
    {
        if ((p.Id != 0 && string.IsNullOrWhiteSpace(p.Sku)) || p.Sku?.Trim().Length > 60) return "El SKU se asigna al crear; al editar conserva uno de hasta 60 caracteres.";
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 200) return "Escribe un nombre de hasta 200 caracteres.";
        if (p.Description?.Length > 1000) return "La descripción admite hasta 1000 caracteres.";
        if (p.CategoryId <= 0 || p.UnitId <= 0) return "Selecciona categoría y unidad de medida.";
        if (p.Price < 0 || p.Price >= 10000000000000000m || decimal.Round(p.Price, 2) != p.Price) return "El precio debe ser positivo o cero y tener hasta dos decimales.";
        if (p.MinimumStock < 0 || p.MinimumStock >= 100000000000000m || decimal.Round(p.MinimumStock, 4) != p.MinimumStock) return "Revisa el stock mínimo: positivo o cero, hasta cuatro decimales.";
        if (p.MaximumStock is { } max && (max < p.MinimumStock || max >= 100000000000000m || decimal.Round(max, 4) != max)) return "El stock máximo debe ser mayor o igual al mínimo, con hasta cuatro decimales.";
        if (p.TracksExpiry && !p.TracksLot) return "El control de vencimiento requiere control de lote.";
        if (p.Id != 0 && p.RowVersion.Length != 8) return "Recarga el producto antes de editarlo.";
        if (p.ExpiryAlertDays is < 0) return "Los días de alerta no pueden ser negativos.";
        return null;
    }
}

