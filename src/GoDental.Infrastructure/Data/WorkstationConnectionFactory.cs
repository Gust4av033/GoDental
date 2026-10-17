using System.Data.Common;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using Dapper;

namespace GoDental.Infrastructure.Data;

public sealed class WorkstationConnectionFactory(SqlConnectionFactory factory, IWorkstationSettingsStore store, IUserSession session) : IDbConnectionFactory
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var profile = await store.LoadAsync(cancellationToken);
        var connection = await factory.OpenAsync(profile, cancellationToken);
        try
        {
            if (session.IsAuthenticated && session is ISessionController bound)
            {
                var scope = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))+N'|'+DB_NAME();", cancellationToken: cancellationToken));
                if (scope != bound.ConnectionScope) throw new InvalidOperationException("La sesión pertenece a otra base. Cierra sesión antes de cambiar la conexión.");
                var active = await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.Usuarios WHERE IdUsuario=@UserId AND Activo=1 AND DebeCambiarClave=0) THEN 1 ELSE 0 END)", new { session.UserId }, cancellationToken: cancellationToken));
                if (!active) throw new UnauthorizedAccessException("La cuenta fue desactivada o requiere cambiar contraseña. Vuelve a iniciar sesión.");
            }
            await connection.ExecuteAsync(new CommandDefinition("""
                EXEC sys.sp_set_session_context @key=N'IdUsuario', @value=@UserId;
                EXEC sys.sp_set_session_context @key=N'IdEquipo', @value=@WorkstationId;
                """, new { session.UserId, session.WorkstationId }, cancellationToken: cancellationToken));
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
}
