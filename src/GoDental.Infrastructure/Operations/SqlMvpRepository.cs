using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository(IDbConnectionFactory factory) : ISalesRepository, ICashRepository, IOrderRepository, IInventorySetupRepository, IFiscalPendingRepository, IMvpOperationRepository, ICommercialParametersRepository, IPurchaseRepository
{
    private const string Authorization = """
        IF NOT EXISTS(SELECT 1 FROM seg.Usuarios WHERE IdUsuario=@userId AND Activo=1 AND DebeCambiarClave=0)
          OR EXISTS(SELECT value FROM OPENJSON(@permissions) EXCEPT
            SELECT p.Codigo FROM seg.UsuarioRoles ur JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1
            JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1 JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 WHERE ur.IdUsuario=@userId)
          THROW 53401,N'El permiso para esta operación ya no está disponible.',1;
        """;
    private sealed record Work(DbConnection Connection, DbTransaction Transaction, CancellationToken Ct)
    {
        public CommandDefinition Cmd(string sql, object? p = null) => new(sql, p, Transaction, cancellationToken: Ct);
        public Task<int> Execute(string sql, object? p = null) => Connection.ExecuteAsync(Cmd(sql, p));
        public async Task<T> Scalar<T>(string sql, object? p = null) => (await Connection.ExecuteScalarAsync<T>(Cmd(sql, p)))!;
        public Task<T> One<T>(string sql, object? p = null) => Connection.QuerySingleAsync<T>(Cmd(sql, p));
        public Task<T?> Maybe<T>(string sql, object? p = null) => Connection.QuerySingleOrDefaultAsync<T>(Cmd(sql, p));
        public async Task<IReadOnlyList<T>> List<T>(string sql, object? p = null) => (await Connection.QueryAsync<T>(Cmd(sql, p))).AsList();
        public Task Authorize(int userId, params string[] permissions) => Execute(Authorization, new { userId, permissions = JsonSerializer.Serialize(permissions) });
    }

    private sealed class RuleFailure(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private static void Require(bool value, string code, string message)
    {
        if (!value)
            throw new RuleFailure(code, message);
    }

    private static void Version(byte[]? current, byte[] expected) => Require(current is not null && current.SequenceEqual(expected), "CONCURRENCY_CONFLICT", "Los datos cambiaron. Consulta su versión actual antes de guardar.");
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static string HashToken(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private async Task<T> Read<T>(int userId, string[] permissions, Func<Work, Task<T>> read, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var w = new Work(c, tx, ct);
        try
        {
            await w.Authorize(userId, permissions);
            var result = await read(w);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (SqlException ex) when (ex.Number == 53401)
        {
            throw new UnauthorizedAccessException(ex.Message, ex);
        }
        catch (RuleFailure ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }
    }

    private sealed class OperationRow
    {
        public string Type { get; set; } = "";
        public int UserId { get; set; }
        public byte[] Hash { get; set; } = [];
        public long Id { get; set; }
        public string State { get; set; } = "";
        public byte[] RecoveryHash { get; set; } = [];
    }

    private Task<Result<MvpReceipt>> Write(Guid operationId, string type, object request, int userId, int? workstationId, string[] permissions, Func<Work, Task<long>> write, CancellationToken ct, OperationTicket? recovery = null) => WriteCore(operationId, type, request, userId, workstationId, permissions, write, ct, recovery);
    private async Task<Result<MvpReceipt>> WriteCore(Guid operationId, string type, object request, int userId, int? workstationId, string[] permissions, Func<Work, Task<long>> write, CancellationToken ct, OperationTicket? recovery)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var w = new Work(c, tx, ct);
        try
        {
            await w.Execute("SET XACT_ABORT ON;");
            await w.Authorize(userId);
            Require(operationId != Guid.Empty, "VALIDATION", "Falta identificador de operación.");
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Json(request)));
            var recoveryHash = recovery is null ? hash : Convert.FromHexString(recovery.RequestHash);
            var locked = await w.Scalar<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=5000;SELECT @r;", new { resource = "GoDental:MVP:" + operationId.ToString("D") });
            Require(locked >= 0, "CONCURRENCY_CONFLICT", "Hay una operación en curso con ese identificador. Consulta el resultado antes de repetir.");
            var previous = await w.Maybe<OperationRow>("SELECT Tipo Type,IdUsuario UserId,HashSolicitud Hash,ISNULL(HashRecuperacion,HashSolicitud) RecoveryHash,EntidadId Id,EstadoResolucion State FROM sis.OperacionesMvp WHERE IdOperacion=@operationId;", new { operationId });
            if (previous is not null)
            {
                Require(previous.Type == type && previous.UserId == userId && (previous.State == "RECHAZADA_DEFINITIVA" ? previous.RecoveryHash.SequenceEqual(recoveryHash) : previous.Hash.SequenceEqual(hash)), "CONCURRENCY_CONFLICT", "Ese identificador ya se utilizó para otra solicitud.");
                if (previous.State == "RECHAZADA_DEFINITIVA")
                    return Result<MvpReceipt>.Failure("OPERATION_REJECTED", "Esta solicitud se cerró definitivamente sin confirmar. Revisa los datos antes de iniciar una nueva.");
                await tx.CommitAsync(ct);
                return Result<MvpReceipt>.Success(new(previous.Id, operationId));
            }

            await w.Authorize(userId, permissions);
            var id = await write(w);
            await w.Execute("INSERT INTO sis.OperacionesMvp(IdOperacion,Tipo,IdUsuario,HashSolicitud,HashRecuperacion,EntidadId) VALUES(@operationId,@type,@userId,@hash,@recoveryHash,@id);", new { operationId, type, userId, hash, recoveryHash, id });
            await w.Execute("""
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosDespuesJson)
                VALUES(@userId,@workstationId,N'MVP',@type,@type,@id,N'Operación confirmada con identificador de reenvío.',@json);
                """, new { userId, workstationId, type, id, json = Json(request) });
            await tx.CommitAsync(ct);
            return Result<MvpReceipt>.Success(new(id, operationId));
        }
        catch (SqlException ex) when (ex.Number is 53501 or 53502) { return Result<MvpReceipt>.Failure("CODE_UNAVAILABLE", ex.Message); }
        catch (RuleFailure ex)
        {
            return Result<MvpReceipt>.Failure(ex.Code, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Result<MvpReceipt>.Failure("VALIDATION", ex.Message);
        }
        catch (SqlException ex) when (ex.Number is 53401 or 53001 or 53201 or 53301)
        {
            return Result<MvpReceipt>.Failure("FORBIDDEN", ex.Message);
        }
        catch (SqlException ex) when (ex.Number is 1205 or 53003 or 53103)
        {
            return Result<MvpReceipt>.Failure("CONCURRENCY_CONFLICT", "Los datos cambiaron o están ocupados. Consulta el estado antes de repetir.");
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Result<MvpReceipt>.Failure("DUPLICATE", "El identificador o código ya está utilizado.");
        }
        catch (SqlException ex) when (ex.Number is 51049 or 51051 or 53004)
        {
            return Result<MvpReceipt>.Failure("INSUFFICIENT_STOCK", ex.Message);
        }
        catch (SqlException ex) when (ex.Number is >= 51001 and <= 51066 || ex.Number is 53002 or 53402)
        {
            return Result<MvpReceipt>.Failure("INVALID_STATE", ex.Message);
        }
    }

    public Task<MvpReceipt?> FindOperationAsync(Guid id, int userId, CancellationToken ct) => Read(userId, [], async w =>
    {
        var row = await w.Maybe<OperationRow>("SELECT EntidadId Id FROM sis.OperacionesMvp WHERE IdOperacion=@id AND IdUsuario=@userId AND EstadoResolucion=N'CONFIRMADA';", new { id, userId });
        return row is null ? null : new MvpReceipt(row.Id, id);
    }, ct);
    private static async Task<MvpPage<T>> Page<T>(Work w, string from, string columns, string where, string order, object p)
    {
        using var rows = await w.Connection.QueryMultipleAsync(w.Cmd($"SELECT COUNT(*) FROM {from} WHERE {where};SELECT {columns} FROM {from} WHERE {where} ORDER BY {order} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;", p));
        var total = await rows.ReadSingleAsync<int>();
        return new((await rows.ReadAsync<T>()).AsList(), total);
    }
}
