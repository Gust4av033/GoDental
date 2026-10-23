namespace GoDental.Domain.Rules;

// Formato de captura; no certifica identidad ni inscripción tributaria.
public static class IdentityFormat
{
    public static string Digits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());
    public static bool IsValid(string? value, bool nit = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Any(c => !char.IsAsciiDigit(c) && c != '-' && c != ' ')) return false;
        var digits = Digits(value);
        return digits.Length == 9 || nit && digits.Length == 14;
    }
    public static string? Normalize(string? value, bool nit = false) => string.IsNullOrWhiteSpace(value) ? null :
        Format(Digits(value), nit && Digits(value).Length == 14 ? 14 : 9);
    public static string Format(string digits, int length)
    {
        var cuts = length == 14 ? new[] { 4, 10, 13 } : new[] { 8 };
        return string.Concat(digits.Select((c, i) => cuts.Contains(i) ? "-" + c : c.ToString()));
    }
}
