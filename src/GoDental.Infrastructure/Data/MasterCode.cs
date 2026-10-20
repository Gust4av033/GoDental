using System.Data.Common;
using Dapper;

namespace GoDental.Infrastructure.Data;

internal static class MasterCode
{
    public static async Task<string> NextAsync(DbConnection connection, DbTransaction transaction, string type, CancellationToken ct) =>
        await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "DECLARE @code nvarchar(60); EXEC sis.sp_SiguienteCodigoMaestro @Tipo=@type,@Codigo=@code OUTPUT; SELECT @code;",
            new { type }, transaction, cancellationToken: ct)) ?? throw new InvalidOperationException("SQL no asignó el código del registro.");
}
