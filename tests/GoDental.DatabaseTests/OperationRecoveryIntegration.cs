using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Security;

internal static class OperationRecoveryIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root, string database)
    {
        var checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; Console.WriteLine("OK " + label); }
        await using var c = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("recovery_test", "Administrador de prueba aislada", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "RECOVERY-TEST")).Value!;
        var repository = new SqlMvpRepository(factory);
        var user = identity.Id; var station = identity.WorkstationId;
        var warehouse = await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        var boxRequest = new CashBoxChange(Guid.NewGuid(), 0, "", "Caja recuperación", warehouse, true, "Prueba aislada");
        var box = await repository.SaveBoxAsync(boxRequest, user, station, default);
        Check(box.IsSuccess, "Prepara caja de recuperación");
        var ticket = OperationIdentity.Describe(boxRequest);
        var resolved = await repository.ResolveOperationAsync(ticket, false, user, station, default);
        Check(resolved.State == OperationResolutionState.Confirmed && resolved.Receipt == box.Value, "Consulta confirmación con huella normalizada");
        Check((await repository.ResolveOperationAsync(ticket, true, user, station, default)).Receipt == box.Value, "Resolver una confirmada nunca la anula");
        var mismatchDenied = false;
        try { await repository.ResolveOperationAsync(ticket with { RequestHash = new string('F', 64) }, true, user, station, default); }
        catch (InvalidOperationException) { mismatchDenied = true; }
        Check(mismatchDenied, "No resuelve otra huella con el mismo GUID");
        var other = await c.ExecuteScalarAsync<int>("INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,Activo,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(N'otro_recovery',N'Otro usuario',N'no-login',1,0);");
        var ownerDenied = false;
        try { await repository.ResolveOperationAsync(ticket, true, other, station, default); }
        catch (InvalidOperationException) { ownerDenied = true; }
        Check(ownerDenied, "Otra cuenta no consume ni cierra un GUID ajeno");

        var delayed = new OpenCashShift(Guid.NewGuid(), checked((int)box.Value!.Id), 15, "Solicitud demorada");
        var delayedTicket = OperationIdentity.Describe(delayed);
        Check((await repository.ResolveOperationAsync(delayedTicket, false, user, station, default)).State == OperationResolutionState.Unconfirmed,
            "Ausencia sin cierre no se presenta como rechazo definitivo");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.OperacionesMvp WHERE IdOperacion=@OperationId;", delayedTicket) == 0, "Consultar ausencia no escribe ledger");
        await using (var tx = await c.BeginTransactionAsync())
        {
            await c.ExecuteAsync("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode=N'Exclusive',@LockOwner=N'Transaction';", new { resource = "GoDental:MVP:" + delayed.OperationId.ToString("D") }, tx);
            Check((await repository.ResolveOperationAsync(delayedTicket, true, user, station, default)).State == OperationResolutionState.InProgress, "Un envío con lock ocupado no se declara rechazado");
            await tx.RollbackAsync();
        }
        Check((await repository.ResolveOperationAsync(delayedTicket, true, user, station, default)).State == OperationResolutionState.RejectedDefinitively, "Cierra ausencia con marca terminal bajo bloqueo");
        Check((await repository.OpenShiftAsync(delayed, user, station, default)).ErrorCode == "OPERATION_REJECTED", "Un envío tardío no confirma después de su cierre");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.TurnosCaja;") == 0 && await repository.FindOperationAsync(delayed.OperationId, user, default) is null, "Rechazo terminal no es turno ni recibo");
        Check((await repository.ResolveOperationAsync(delayedTicket, true, user, station, default)).State == OperationResolutionState.RejectedDefinitively, "Repetir resolución es idempotente");

        // Both contenders use the same database application lock. Exactly one terminal outcome wins.
        var racing = boxRequest with { OperationId = Guid.NewGuid(), Name = "Carrera resolución" };
        var before = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Cajas;");
        var sending = repository.SaveBoxAsync(racing, user, station, default);
        var resolving = repository.ResolveOperationAsync(OperationIdentity.Describe(racing), true, user, station, default);
        await Task.WhenAll(sending, resolving);
        var final = await repository.ResolveOperationAsync(OperationIdentity.Describe(racing), true, user, station, default);
        Check(final.State is OperationResolutionState.Confirmed or OperationResolutionState.RejectedDefinitively, "Carrera termina en un único estado terminal");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Cajas;") == before + (final.State == OperationResolutionState.Confirmed ? 1 : 0), "Resolver y enviar concurrentes no duplican efectos");
        Check((await repository.SaveBoxAsync(racing, user, station, default)).IsSuccess == (final.State == OperationResolutionState.Confirmed), "Reenvío respeta el ganador terminal");

        // Audit failure must roll back the fence too, otherwise a caller would see a false terminal state.
        var rollback = OperationIdentity.Describe(boxRequest with { OperationId = Guid.NewGuid() });
        var failed = false;
        try { await repository.ResolveOperationAsync(rollback, true, user, int.MaxValue, default); }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 547) { failed = true; }
        Check(failed && (await repository.ResolveOperationAsync(rollback, false, user, station, default)).State == OperationResolutionState.Unconfirmed, "Fallo de auditoría revierte la marca terminal");

        // Recovering an existing result is not a new business authorization.
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'AJUSTES.EDITAR');");
        Check((await repository.SaveBoxAsync(boxRequest, user, station, default)).Value == box.Value, "Recupera recibo confirmado aunque se revocó permiso comercial");
        Check((await repository.ResolveOperationAsync(ticket, false, user, station, default)).Receipt == box.Value, "La resolución propia sigue disponible sin permiso de venta");
        Check((await repository.SaveBoxAsync(boxRequest with { OperationId = Guid.NewGuid() }, user, station, default)).ErrorCode == "FORBIDDEN", "Revocar permiso sí impide nuevos efectos");

        var folder = Path.Combine(Path.GetTempPath(), "GoDentalRecoveryProbe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var crashBeforeCommit = boxRequest with { OperationId = Guid.NewGuid(), Name = "Cierre antes del envío" };
            var pending = new PendingOperation(identity.ConnectionScope, user, station, OperationIdentity.Describe(crashBeforeCommit), DateTimeOffset.UtcNow);
            // Child is killed after durable preparation, before receiving a result. Its journal survives.
            await KillPreparedChild(folder, pending);
            var store = new FilePendingOperationStore(folder);
            Check((await store.LoadAsync(pending.Scope, user, station)).Single() == pending, "Matar proceso después del flush conserva ticket y propietario");
            var session = new UserSession(); session.Begin(identity);
            var restarted = new MvpOperationService(repository, session, store);
            Check((await restarted.ResolveAsync(pending, true)).State == OperationResolutionState.RejectedDefinitively && (await restarted.PendingAsync()).Count == 0,
                "Reinicio antes del commit cierra la solicitud y limpia sin enviarla");
            Check((await repository.SaveBoxAsync(crashBeforeCommit, user, station, default)).ErrorCode == "OPERATION_REJECTED", "Un envío posterior al reinicio respeta el cierre definitivo");
            await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'AJUSTES.EDITAR');");
            var crashAfterCommit = boxRequest with { OperationId = Guid.NewGuid(), Name = "Cierre después del commit" };
            var committedPending = pending with { Ticket = OperationIdentity.Describe(crashAfterCommit), Created = DateTimeOffset.UtcNow };
            MvpReceipt? committedReceipt = null;
            await KillPreparedChild(folder, committedPending, async () => committedReceipt = (await repository.SaveBoxAsync(crashAfterCommit, user, station, default)).Value);
            var recovered = new MvpOperationService(repository, session, new FilePendingOperationStore(folder));
            Check((await recovered.ResolveAsync((await recovered.PendingAsync()).Single(), true)).Receipt == committedReceipt && committedReceipt is not null && (await recovered.PendingAsync()).Count == 0,
                "Reinicio tras commit recupera el mismo recibo sin escritura comercial");
        }
        finally { Directory.Delete(folder, true); }

        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
        Check((await repository.ResolveOperationAsync(delayedTicket, false, user, station, default)).State == OperationResolutionState.RejectedDefinitively
            && (await repository.ResolveOperationAsync(ticket, false, user, station, default)).Receipt == box.Value, "Reaplicar 015 conserva ambos estados terminales");
        Console.WriteLine($"{checks} comprobaciones de recuperación SQL correctas.");
    }

    private static async Task KillPreparedChild(string folder, PendingOperation pending, Func<Task>? afterPreparation = null)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); start.ArgumentList.Add("--recovery-journal-probe"); start.ArgumentList.Add(folder);
        start.ArgumentList.Add(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(pending)));
        using var child = Process.Start(start) ?? throw new InvalidOperationException("No inició el proceso aislado.");
        try
        {
            var ready = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            if (ready != "JOURNAL_READY") throw new InvalidOperationException("El proceso no confirmó preparación durable.");
            if (afterPreparation is not null) await afterPreparation();
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
