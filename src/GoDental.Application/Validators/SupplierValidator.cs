using System.Net.Mail;
using GoDental.Application.DTOs;
using GoDental.Domain.Rules;

namespace GoDental.Application.Validators;

public static class SupplierValidator
{
    public static string? ValidateFields(SupplierData supplier, SupplierData? previous) => DirectoryFields.SupplierError(supplier, previous);
    public static string? Validate(SupplierChange request)
    {
        var s = request.Supplier;
        if (s is null || s.Id < 0 || (s.Id > 0 && s.RowVersion?.Length != 8) || (s.Id == 0 && s.RowVersion?.Length != 0))
            return "Recarga la ficha del proveedor antes de guardar.";
        if ((s.Id != 0 && string.IsNullOrWhiteSpace(s.Code)) || s.Code?.Trim().Length > 30 || string.IsNullOrWhiteSpace(s.Name) || s.Name.Trim().Length > 200)
            return "Revisa el código (30) y el nombre obligatorio (200 caracteres).";
        if (s.Country?.Trim().Length > 80 || s.Address?.Trim().Length > 400 || s.Phone?.Trim().Length > 40)
            return "Revisa país (80), dirección (400) y teléfono (40 caracteres).";
        if (EmailError(s.Email) is { } emailError) return emailError;
        if (!string.IsNullOrWhiteSpace(s.Website) && (s.Website.Trim().Length > 300
            || !Uri.TryCreate(s.Website.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0)) return "La web debe ser una URL HTTP/HTTPS sin credenciales y de hasta 300 caracteres.";
        return ReasonError(request.Reason);
    }

    public static string? Validate(SupplierContactChange request)
    {
        if (VersionError(request.SupplierId, request.ExpectedSupplierVersion) is { } versionError) return versionError;
        var c = request.Contact;
        if (c is null || c.Id < 0 || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 150
            || c.Position?.Trim().Length > 100 || c.Phone?.Trim().Length > 40 || (c.Primary && !c.Active))
            return "Revisa el contacto: nombre (150), cargo (100), teléfono (40); un principal debe estar activo.";
        return EmailError(c.Email) ?? ReasonError(request.Reason);
    }

    public static string? Validate(SupplierProductChange request)
    {
        if (VersionError(request.SupplierId, request.ExpectedSupplierVersion) is { } versionError) return versionError;
        var p = request.Product;
        if (p is null || p.ProductId <= 0 || p.SupplierSku?.Trim().Length > 100)
            return "Selecciona un producto y revisa el SKU del proveedor (100 caracteres).";
        if (p.LastCost is { } cost && (cost < 0 || cost > 999999999999.999999m || decimal.Round(cost, 6) != cost || p.CurrencyId is null))
            return "El costo requiere moneda, no puede ser negativo y admite hasta seis decimales.";
        if (p.CurrencyId is <= 0 || p.LeadTimeDays is < 0 || p.MinimumQuantity is { } minimum && (minimum <= 0 || !InventoryCountRules.IsValidQuantity(minimum)))
            return "Revisa moneda, plazo y cantidad mínima positiva (hasta cuatro decimales).";
        return ReasonError(request.Reason);
    }

    private static string? VersionError(int supplierId, byte[]? version) => supplierId <= 0 || version?.Length != 8
        ? "Recarga el proveedor antes de modificar sus contactos o productos." : null;
    private static string? ReasonError(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500
        ? "Escribe un motivo de hasta 500 caracteres." : null;
    private static string? EmailError(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        email = email.Trim();
        return email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email
            ? "Escribe un correo válido de hasta 254 caracteres." : null;
    }
}
