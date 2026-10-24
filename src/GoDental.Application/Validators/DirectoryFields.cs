using GoDental.Application.DTOs;
using GoDental.Application.Localization;
using GoDental.Domain.Rules;
namespace GoDental.Application.Validators;

public static class DirectoryFields
{
    public static string? CustomerError(CustomerData c, CustomerData? old)
    {
        if ((old is null || c.Dui != old.Dui) && !IdentityFormat.IsValid(c.Dui)) return "El DUI requiere 9 dígitos: 00000000-0.";
        if ((old is null || c.Nit != old.Nit) && !IdentityFormat.IsValid(c.Nit, true)) return "El NIT requiere 14 dígitos (0000-000000-000-0), o 9 si corresponde a DUI como NIT.";
        return PhoneFormat.Error(c.Phone, old?.Phone, "SV", old is null) ?? PhoneFormat.Error(c.WhatsApp, old?.WhatsApp, "SV", old is null);
    }
    public static CustomerData Normalize(CustomerData c, CustomerData? old) => c with {
        Dui = old is not null && c.Dui == old.Dui ? c.Dui : IdentityFormat.Normalize(c.Dui),
        Nit = old is not null && c.Nit == old.Nit ? c.Nit : IdentityFormat.Normalize(c.Nit, true),
        Phone = PhoneFormat.NormalizeChanged(c.Phone, old?.Phone, "SV", old is null),
        WhatsApp = PhoneFormat.NormalizeChanged(c.WhatsApp, old?.WhatsApp, "SV", old is null) };
    public static string? ContactError(CustomerContactData c, CustomerContactData? old) => PhoneFormat.Error(c.Phone, old?.Phone, "SV", old is null) ?? PhoneFormat.Error(c.WhatsApp, old?.WhatsApp, "SV", old is null);
    public static CustomerContactData Normalize(CustomerContactData c, CustomerContactData? old) => c with { Phone = PhoneFormat.NormalizeChanged(c.Phone, old?.Phone, "SV", old is null), WhatsApp = PhoneFormat.NormalizeChanged(c.WhatsApp, old?.WhatsApp, "SV", old is null) };
    public static string? AddressError(CustomerAddressData a, CustomerAddressData? old) => old is not null && a.Department == old.Department && a.Municipality == old.Municipality && a.District == old.District ? null : DirectoryCatalog.LocationError(a.Department, a.Municipality, a.District);
    public static string? SupplierError(SupplierData s, SupplierData? old)
    {
        if ((old is null || s.Country != old.Country) && !string.IsNullOrWhiteSpace(s.Country) && DirectoryCatalog.Country(s.Country) is null) return "Selecciona un país del catálogo.";
        return PhoneFormat.Error(s.Phone, old?.Phone, DirectoryCatalog.Country(s.Country)?.Code, old is null || s.Country != old.Country);
    }
    public static SupplierData Normalize(SupplierData s, SupplierData? old) => s with { Phone = PhoneFormat.NormalizeChanged(s.Phone, old?.Phone, DirectoryCatalog.Country(s.Country)?.Code, old is null || s.Country != old.Country) };
}
