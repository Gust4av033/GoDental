using System.Text.RegularExpressions;

namespace GoDental.Application.Validators;

public static class IdentityValidator
{
    public static string? ValidateUser(string userName, string displayName)
    {
        if (!Regex.IsMatch(userName, @"^[A-Za-z0-9._-]{3,60}$")) return "Usa un usuario de 3 a 60 letras, números, punto, guion o guion bajo.";
        if (displayName.Trim().Length is < 2 or > 150) return "Escribe un nombre de 2 a 150 caracteres.";
        return null;
    }
    public static string? ValidatePassword(string userName, string password)
    {
        if (password.Length is < 10 or > 128) return "Usa una contraseña o frase de 10 a 128 caracteres.";
        if (string.IsNullOrWhiteSpace(password) || password.Equals(userName, StringComparison.OrdinalIgnoreCase) ||
            new[] { "1234567890", "password123", "contraseña", "qwerty12345" }.Contains(password, StringComparer.OrdinalIgnoreCase))
            return "Elige una contraseña menos predecible y distinta del usuario.";
        return null;
    }
}
