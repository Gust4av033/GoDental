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
        return null;
    }
}
