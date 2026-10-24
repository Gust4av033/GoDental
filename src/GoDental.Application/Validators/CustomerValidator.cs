using System.Net.Mail;
using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;

public static class CustomerValidator
{
    public static string? Validate(CustomerChange request)
    {
        var c = request.Customer;
        if (c is null || c.Id < 0 || (c.Id > 0 && c.RowVersion?.Length != 8) || (c.Id == 0 && c.RowVersion?.Length != 0)) return "Recarga la ficha antes de guardar.";
        if ((c.Id != 0 && string.IsNullOrWhiteSpace(c.Code)) || c.Code?.Trim().Length > 30 || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 200 || c.TypeId <= 0 || c.PriceListId is <= 0) return "Revisa código (30), nombre (200), tipo de cliente y lista de precios.";
        if (c.CommercialName?.Trim().Length > 200 || c.Nit?.Trim().Length > 30 || c.Nrc?.Trim().Length > 30 || c.Dui?.Trim().Length > 20 || c.BusinessActivity?.Trim().Length > 250) return "Revisa las longitudes del nombre comercial y datos fiscales.";
        if (c.Phone?.Trim().Length > 30 || c.WhatsApp?.Trim().Length > 30) return "Teléfono y WhatsApp admiten hasta 30 caracteres.";
        return Email(c.Email) ?? Reason(request.Reason);
    }
    public static string? Validate(CustomerContactChange request)
    {
        if (Version(request.CustomerId, request.ExpectedVersion) is { } error) return error;
        var c = request.Contact;
        if (c is null || c.Id < 0 || string.IsNullOrWhiteSpace(c.Name) || c.Name.Trim().Length > 150 || c.Position?.Trim().Length > 100 || c.Phone?.Trim().Length > 30 || c.WhatsApp?.Trim().Length > 30) return "Revisa nombre, cargo y teléfonos del contacto.";
        return Email(c.Email) ?? Reason(request.Reason);
    }
    public static string? Validate(CustomerAddressChange request)
    {
        if (Version(request.CustomerId, request.ExpectedVersion) is { } error) return error;
        var a = request.Address;
        if (a is null || a.Id < 0 || a.Type is not ("FISCAL" or "ENTREGA" or "OTRA") || string.IsNullOrWhiteSpace(a.Address) || a.Address.Trim().Length > 500 || a.Department?.Trim().Length > 100 || a.Municipality?.Trim().Length > 100 || a.District?.Trim().Length > 100 || a.Reference?.Trim().Length > 300) return "Revisa tipo, dirección (500), departamento/municipio (100) y referencia (300).";
        return Reason(request.Reason);
    }
    public static string? Validate(CustomerCreditChange request)
    {
        if (Version(request.CustomerId, request.ExpectedVersion) is { } error) return error;
        var c = request.Credit;
        if (c is null || c.Limit is { } n && (n < 0 || n > 9999999999999999.99m || decimal.Round(n, 2) != n) || c.Days is < 0 || c.NoticeDays is <= 0 || c.BlockReason?.Trim().Length > 500) return "Revisa límite no negativo (dos decimales), plazo e intervalo de avisos.";
        if (c.Blocked && string.IsNullOrWhiteSpace(c.BlockReason)) return "Escribe el motivo del bloqueo de crédito.";
        return Reason(request.Reason);
    }
    public static string? Version(long id, byte[]? version) => id <= 0 || version?.Length != 8 ? "Recarga el cliente antes de guardar." : null;
    private static string? Reason(string? reason) => string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 ? "Escribe un motivo de hasta 500 caracteres." : null;
    private static string? Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var value = email.Trim();
        return value.Length > 254 || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value ? "Escribe un correo válido de hasta 254 caracteres." : null;
    }
}
