namespace GoDental.Application.Validators;

public static class BarcodeValidator
{
    public static string? Validate(string? code, string type)
    {
        if (string.IsNullOrWhiteSpace(code)) return type == "CODE128" ? null : "El código generado internamente utiliza CODE128.";
        code = code.Trim();
        if (code.Length > 80 || code.Any(c => c < ' ' || c > '~')) return "Usa un código de hasta 80 caracteres ASCII imprimibles.";
        if (type is "EAN13" or "UPC")
        {
            var length = type == "EAN13" ? 13 : 12;
            if (code.Length != length || code.Any(c => c is < '0' or > '9')) return $"{type} requiere {length} dígitos.";
            var sum = 0;
            for (var i = code.Length - 2; i >= 0; i--) sum += (code[i] - '0') * ((code.Length - 2 - i) % 2 == 0 ? 3 : 1);
            if ((10 - sum % 10) % 10 != code[^1] - '0') return "El dígito de control del código no es válido.";
        }
        return null;
    }
}
