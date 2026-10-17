using GoDental.Application.DTOs;

namespace GoDental.Application.Validators;

public static class WorkstationSettingsValidator
{
    public static string? Validate(WorkstationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Server) || string.IsNullOrWhiteSpace(settings.Database))
            return "Escribe el servidor y el nombre de la base de datos.";
        if (settings.Server.Length > 256 || settings.Database.Length > 128)
            return "Revisa la longitud del servidor y del nombre de la base de datos.";
        if (settings.Server.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            return "Usa una instancia de SQL Server compartida; LocalDB no es la base central de GoDental.";
        if (settings.LocalDevelopment && (!IsLocalServer(settings.Server) || !System.Text.RegularExpressions.Regex.IsMatch(settings.Database, @"^GoDental_(Development|Tests_[A-Za-z0-9]+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
            return "El certificado de desarrollo sólo se admite en este equipo y en una base GoDental_ separada.";
        return null;
    }

    public static bool IsLocalServer(string server) => new[] { ".", "localhost", Environment.MachineName }
        .Contains(server.Trim(), StringComparer.OrdinalIgnoreCase);
}
