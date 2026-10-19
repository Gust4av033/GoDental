using Dapper;
using GoDental.Application.DTOs;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.Validators;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace GoDental.Infrastructure.Data;

public sealed class SqlConnectionHealthCheck(SqlConnectionFactory factory, ILogger<SqlConnectionHealthCheck> logger)
    : IConnectionHealthCheck
{
    public async Task<ConnectionCheckResult> CheckAsync(WorkstationSettings settings, CancellationToken cancellationToken = default)
    {
        if (WorkstationSettingsValidator.Validate(settings) is { } error)
            return new(false, error);
        try
        {
            await using var connection = await factory.OpenAsync(settings, cancellationToken);
            var command = new CommandDefinition("SELECT 1;", commandTimeout: 5, cancellationToken: cancellationToken);
            var value = await connection.ExecuteScalarAsync<int>(command);
            return new(value == 1, value == 1
                ? "Conexión comprobada. El servidor respondió sin modificar datos."
                : "El servidor no devolvió la respuesta esperada.");
        }
        catch (SqlException ex)
        {
            // Sólo número y tipo: no registrar endpoints, credenciales ni cadenas de conexión.
            logger.LogWarning("Falló el diagnóstico SQL. Código {SqlNumber}; tipo {ErrorType}.", ex.Number, ex.GetType().Name);
            return new(false, ex.Number switch
            {
                18456 => "Tu cuenta de Windows no tiene acceso a este servidor SQL.",
                4060 => "No se pudo abrir esa base. Revisa su nombre y los permisos de tu cuenta.",
                -2 => "El servidor tardó demasiado en responder. Revisa la red e inténtalo de nuevo.",
                _ => "No se pudo conectar. Revisa servidor, red, permisos y certificado de SQL Server."
            });
        }
    }
}
