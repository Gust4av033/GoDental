using System.Data;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Operations;

public sealed partial class SqlMvpRepository
{
    public async Task<OperationResolution> ResolveOperationAsync(OperationTicket ticket, bool rejectIfAbsent, int userId, int? workstationId, CancellationToken ct)
    {
        if (!OperationIdentity.IsValid(ticket)) throw new ArgumentException("Identidad de solicitud inválida.");
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var w = new Work(c, tx, ct);
        try
        {
            await w.Execute("SET XACT_ABORT ON;");
            await w.Authorize(userId);
            var locked = await w.Scalar<int>("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=0;SELECT @r;", new { resource = "GoDental:MVP:" + ticket.OperationId.ToString("D") });
            if (locked == -1) return new(OperationResolutionState.InProgress);
            Require(locked >= 0, "CONCURRENCY_CONFLICT", "No se pudo resolver la solicitud. Consulta de nuevo.");
            var row = await w.Maybe<OperationRow>("SELECT Tipo Type,IdUsuario UserId,ISNULL(HashRecuperacion,HashSolicitud) RecoveryHash,EntidadId Id,EstadoResolucion State FROM sis.OperacionesMvp WHERE IdOperacion=@OperationId;", ticket);
            if (row is not null)
            {
                Require(row.UserId == userId && row.Type == ticket.Type && Convert.ToHexString(row.RecoveryHash) == ticket.RequestHash,
                    "FORBIDDEN", "La solicitud no corresponde a este usuario o contenido.");
                await tx.CommitAsync(ct);
                return row.State == "CONFIRMADA" ? new(OperationResolutionState.Confirmed, new(row.Id, ticket.OperationId)) : new(OperationResolutionState.RejectedDefinitively);
            }
            if (!rejectIfAbsent) return new(OperationResolutionState.Unconfirmed);
            var hash = Convert.FromHexString(ticket.RequestHash);
            await w.Execute("INSERT INTO sis.OperacionesMvp(IdOperacion,Tipo,IdUsuario,HashSolicitud,HashRecuperacion,EntidadId,EstadoResolucion) VALUES(@OperationId,@Type,@userId,@hash,@hash,0,N'RECHAZADA_DEFINITIVA');", new { ticket.OperationId, ticket.Type, userId, hash });
            await w.Execute("""
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosDespuesJson)
                VALUES(@userId,@workstationId,N'MVP',N'CERRAR_SOLICITUD',N'OperacionMvp',0,
                N'Solicitud cerrada definitivamente sin efectos comerciales por su usuario.',@json);
                """, new { userId, workstationId, json = Json(ticket) });
            await tx.CommitAsync(ct);
            return new(OperationResolutionState.RejectedDefinitively);
        }
        catch (SqlException ex) when (ex.Number == 53401) { throw new UnauthorizedAccessException("No tienes acceso para resolver esta solicitud.", ex); }
        catch (RuleFailure ex) { throw new InvalidOperationException(ex.Message, ex); }
    }
}
