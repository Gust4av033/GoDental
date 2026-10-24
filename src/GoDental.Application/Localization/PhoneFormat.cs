using PhoneNumbers;
namespace GoDental.Application.Localization;

public static class PhoneFormat
{
    public static bool TryFormat(string? value, string? region, out string? formatted, out string hint)
    {
        formatted = value; hint = "Código internacional opcional: usa + seguido del código y número.";
        if (string.IsNullOrWhiteSpace(value)) { formatted = null; return true; }
        var text = value.Trim();
        if (text.Length > 40 || text.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')')))
        { hint = "Usa dígitos, espacios, guiones y un + inicial opcional."; return false; }
        if (text.Count(c => c == '+') > 1 || text.Contains('+') && !text.StartsWith('+'))
        { hint = "El signo + sólo se admite al inicio."; return false; }
        var util = PhoneNumberUtil.GetInstance();
        if (!text.StartsWith('+') && (region is null || !util.GetSupportedRegions().Contains(region)))
        { hint = "Selecciona el país para un número nacional o escribe el código internacional con +."; return false; }
        try
        {
            var phone = util.Parse(text, region ?? "ZZ");
            if (!text.StartsWith('+') && phone.CountryCode != util.GetCountryCodeForRegion(region!))
            { hint = "Para un teléfono de otro país escribe + y su código internacional."; return false; }
            if (!util.IsPossibleNumber(phone)) { hint = "La longitud del teléfono no corresponde al país; revisa el número o agrega + y código internacional."; return false; }
            formatted = util.Format(phone, text.StartsWith('+') ? PhoneNumberFormat.INTERNATIONAL : PhoneNumberFormat.NATIONAL);
            var detected = util.GetRegionCodeForNumber(phone);
            var name = DirectoryCatalog.Country(detected)?.Name ?? $"código +{phone.CountryCode}";
            hint = $"Número de {name} (+{phone.CountryCode}). El prefijo internacional es opcional.";
            return true;
        }
        catch (NumberParseException) { hint = "Revisa el teléfono y el código internacional opcional (+)."; return false; }
    }
    public static string? Error(string? value, string? original, string? region, bool isNew = false) => !isNew && value == original ? null : TryFormat(value, region, out _, out var hint) ? null : hint;
    public static string? NormalizeChanged(string? value, string? original, string? region, bool isNew = false) => !isNew && value == original ? value : TryFormat(value, region, out var formatted, out _) ? formatted : value;
}
