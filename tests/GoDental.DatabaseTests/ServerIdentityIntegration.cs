using System.Data.Common;
using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class ServerIdentityIntegration
{
    public static async Task RunAsync(IDbConnectionFactory ownerFactory, string root, string database)
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks++; Console.WriteLine("OK " + name);
        }
        var password = "synthetic-only-" + Guid.NewGuid().ToString("N");
        var hasher = new Pbkdf2PasswordHasher();
        var identities = new SqlIdentityGateway(ownerFactory, hasher);
        var created = await identities.CreateAdministratorAsync("server.operator", "Operador sintético", password, "SERVER-TEST-PC");
        Check(created.IsSuccess, "Usuario y estación de 026 preparados antes de actualizar");
        var userId = created.Value!.Id; var station = created.Value.WorkstationId;
        await using var owner = await ownerFactory.OpenConnectionAsync();
        Check(await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables") == 81,
            "La actualización comienza realmente en 026, sin tabla de sesiones");
        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root,"database"));
        Check(await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.Usuarios WHERE IdUsuario=@userId AND Usuario=N'server.operator'", new { userId }) == 1,
            "027 conserva el usuario y datos preexistentes");
        await owner.ExecuteAsync("CREATE USER ServerIdentityTests WITHOUT LOGIN; ALTER ROLE GoDentalServerRuntime ADD MEMBER ServerIdentityTests;");
        var limited = new LimitedFactory(database);
        ServerAuthenticationService Service() => new(new SqlServerAuthenticationRepository(limited), hasher);
        var service = Service();
        ServerLoginRequest Request(string? pass = null, string workstation = "SERVER-TEST-PC") =>
            new() { UserName = "server.operator", Password = pass ?? password, Workstation = workstation };
        async Task<ServerAccessGrant> Login(string workstation = "SERVER-TEST-PC")
        {
            var result = await service.SignInAsync(Request(workstation: workstation));
            if (!result.IsSuccess) throw new InvalidOperationException("La fixture no pudo iniciar sesión.");
            return result.Value!;
        }
        static byte[] Hash(ServerAccessGrant grant) => SHA256.HashData(Convert.FromHexString(grant.AccessToken));

        var ownerRejected = false;
        try { await ServerSqlConnectionFactory.VerifyRuntimeIdentityAsync(owner); }
        catch (UnauthorizedAccessException) { ownerRejected = true; }
        Check(ownerRejected, "El runtime rechaza la identidad propietaria de instalación");
        await using (var runtime = await limited.OpenConnectionAsync())
        {
            async Task<bool> Denied(string command)
            {
                try { await runtime.ExecuteAsync(command); return false; }
                catch (SqlException ex) when (ex.Number == 229) { return true; }
            }
            Check(await Denied("SELECT PasswordHash FROM seg.Usuarios;"), "El servicio no puede leer hashes por acceso directo a tabla");
            Check(await Denied("SELECT * FROM fis.DocumentosDte;"), "El rol de identidad no puede leer documentos fiscales");
            Check(await Denied("UPDATE seg.Usuarios SET Activo=0 WHERE 1=0;"), "El servicio no puede modificar usuarios directamente");
            Check(await Denied("EXEC seg.sp_ObtenerPermisosUsuario @IdUsuario=1;"), "El servicio no puede ejecutar procedimientos históricos fuera de su alcance");
            Check(await runtime.ExecuteScalarAsync<int>("SELECT IS_MEMBER(N'GoDentalServerRuntime');") == 1,
                "Las pruebas usan el rol SQL limitado real, no el propietario");
        }
        await owner.ExecuteAsync("GRANT SELECT ON OBJECT::seg.Usuarios TO ServerIdentityTests;");
        var broadRejected = false;
        try { await using var unused = await limited.OpenConnectionAsync(); }
        catch (UnauthorizedAccessException) { broadRejected = true; }
        Check(broadRejected, "Un grant directo adicional invalida la identidad runtime");
        await owner.ExecuteAsync("REVOKE SELECT ON OBJECT::seg.Usuarios FROM ServerIdentityTests;");

        await owner.ExecuteAsync("DENY EXECUTE ON OBJECT::seg.ServidorSesionRevocar TO ServerIdentityTests;");
        var incompleteRejected = false;
        try { await using var unused = await limited.OpenConnectionAsync(); }
        catch (UnauthorizedAccessException) { incompleteRejected = true; }
        Check(incompleteRejected, "Pertenecer al rol no basta si falta un EXECUTE de 027");
        await owner.ExecuteAsync("REVOKE EXECUTE ON OBJECT::seg.ServidorSesionRevocar FROM ServerIdentityTests;");
        await using (var restored = await limited.OpenConnectionAsync())
            Check(restored.State == System.Data.ConnectionState.Open, "Restaurar el permiso 027 completo permite la identidad limitada");

        var first = await Login();
        Check(first.AccessToken.Length == 64 && first.ExpiresAt > DateTimeOffset.UtcNow.AddHours(7),
            "Autenticación PBKDF2 real entrega sesión opaca con expiración absoluta");
        var stored = await owner.QuerySingleAsync<byte[]>("SELECT TokenHash FROM seg.SesionesServidor;");
        Check(stored.SequenceEqual(Hash(first)) && !stored.SequenceEqual(Convert.FromHexString(first.AccessToken)),
            "SQL guarda sólo el hash del token, sin credencial Bearer reutilizable");
        var profile = await Service().AuthenticateAsync(first.AccessToken);
        Check(profile is { UserId: > 0 } && profile.UserId == userId && profile.WorkstationId == station && profile.Permissions.Contains("FISCAL.VER"),
            "Otra instancia recupera identidad y permisos actuales desde SQL");

        var role = await owner.ExecuteScalarAsync<int>("SELECT IdRol FROM seg.Roles WHERE Nombre=N'Administrador';");
        await owner.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdRol=@role AND IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'FISCAL.VER');", new { role });
        Check(!(await service.AuthenticateAsync(first.AccessToken))!.Permissions.Contains("FISCAL.VER"),
            "Revocar permiso se refleja sin renovar token ni reiniciar host");
        await owner.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdRol=@role AND IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'FISCAL.VER');", new { role });

        var unknown = await service.SignInAsync(new() { UserName = "missing.user", Password = password, Workstation = "SERVER-TEST-PC" });
        var incorrect = await service.SignInAsync(Request("synthetic-wrong-password"));
        Check(!unknown.IsSuccess && !incorrect.IsSuccess && unknown.Message == incorrect.Message,
            "Usuario inexistente y contraseña incorrecta tienen respuesta genérica idéntica");
        var stationCount = await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.EquiposTrabajo;");
        Check(!(await service.SignInAsync(Request(workstation: "UNREGISTERED-PC"))).IsSuccess &&
            await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.EquiposTrabajo;") == stationCount,
            "Login HTTP no crea ni habilita estaciones nuevas");

        var replacement = await Login();
        Check(await service.AuthenticateAsync(first.AccessToken) is null && await service.AuthenticateAsync(replacement.AccessToken) is not null,
            "Nuevo login del mismo par revoca de forma durable la sesión anterior");
        await service.SignOutAsync(replacement.AccessToken); await service.SignOutAsync(replacement.AccessToken);
        Check(await Service().AuthenticateAsync(replacement.AccessToken) is null &&
            await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Accion=N'SERVIDOR_LOGOUT';") == 1,
            "Logout idempotente y revocación sobreviven a nueva instancia");

        var changeRequired = await Login();
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET DebeCambiarClave=1 WHERE IdUsuario=@userId;", new { userId });
        Check(await service.AuthenticateAsync(changeRequired.AccessToken) is null &&
            (await service.SignInAsync(Request())).ErrorCode == "PASSWORD_CHANGE_REQUIRED",
            "Cambio obligatorio invalida la sesión e impide obtener otro token");
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET DebeCambiarClave=0 WHERE IdUsuario=@userId;", new { userId });
        Check(await service.AuthenticateAsync(changeRequired.AccessToken) is null,
            "La sesión invalidada no revive al retirar el requisito");

        var changedPassword = await Login();
        var originalHash = await owner.ExecuteScalarAsync<string>("SELECT PasswordHash FROM seg.Usuarios WHERE IdUsuario=@userId;", new { userId });
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET PasswordHash=@newHash WHERE IdUsuario=@userId;", new { userId, newHash = hasher.Hash("synthetic-new-password-" + Guid.NewGuid()) });
        Check(await service.AuthenticateAsync(changedPassword.AccessToken) is null, "Cambio de credencial invalida el fingerprint de sesión");
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET PasswordHash=@originalHash WHERE IdUsuario=@userId;", new { userId, originalHash });
        Check(await service.AuthenticateAsync(changedPassword.AccessToken) is null, "Una credencial restaurada no revive la sesión revocada");

        foreach (var target in new[] { "user", "station" })
        {
            var token = await Login();
            var sql = target == "user" ? "UPDATE seg.Usuarios SET Activo=@active WHERE IdUsuario=@id;" : "UPDATE sis.EquiposTrabajo SET Activo=@active WHERE IdEquipo=@id;";
            var id = target == "user" ? userId : station;
            await owner.ExecuteAsync(sql, new { active = false, id });
            Check(await service.AuthenticateAsync(token.AccessToken) is null, "Desactivación de " + target + " invalida sesión al consultar");
            await owner.ExecuteAsync(sql, new { active = true, id });
            Check(await service.AuthenticateAsync(token.AccessToken) is null, "Reactivación de " + target + " no revive una sesión invalidada");
        }

        var idle = await Login();
        await owner.ExecuteAsync("UPDATE seg.SesionesServidor SET CreadaEn=DATEADD(minute,-30,SYSDATETIMEOFFSET()),UltimoUso=DATEADD(minute,-21,SYSDATETIMEOFFSET()) WHERE TokenHash=@hash;", new { hash = Hash(idle) });
        Check(await service.AuthenticateAsync(idle.AccessToken) is null, "Inactividad superior a veinte minutos impide acceso");
        var expired = await Login();
        await owner.ExecuteAsync("UPDATE seg.SesionesServidor SET CreadaEn=DATEADD(hour,-10,SYSDATETIMEOFFSET()),UltimoUso=DATEADD(minute,-1,SYSDATETIMEOFFSET()),ExpiraEn=DATEADD(hour,-1,SYSDATETIMEOFFSET()) WHERE TokenHash=@hash;", new { hash = Hash(expired) });
        Check(await service.AuthenticateAsync(expired.AccessToken) is null, "Expiración absoluta no se prolonga con actividad reciente");

        var lockedSession = await Login();
        var failed = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Service().SignInAsync(Request("synthetic-concurrent-wrong"))));
        Check(failed.All(result => !result.IsSuccess) &&
            await owner.ExecuteScalarAsync<int>("SELECT IntentosFallidos FROM seg.Usuarios WHERE IdUsuario=@userId;", new { userId }) == 5,
            "Cinco fallos concurrentes se contabilizan transaccionalmente");
        Check(!(await service.SignInAsync(Request())).IsSuccess && await service.AuthenticateAsync(lockedSession.AccessToken) is null,
            "Cuenta bloqueada rechaza contraseña correcta e invalida sesión previa");
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET BloqueadoHasta=NULL,IntentosFallidos=0 WHERE IdUsuario=@userId;", new { userId });

        var credentialRepo = new SqlServerAuthenticationRepository(limited);
        var stale = (await credentialRepo.ReadCredentialAsync("server.operator", default))!;
        await owner.ExecuteAsync("UPDATE seg.Usuarios SET NombreCompleto=N'Nombre modificado concurrentemente' WHERE IdUsuario=@userId;", new { userId });
        var staleDecision = await credentialRepo.CompleteSignInAsync("server.operator", stale.RowVersion, true,
            "SERVER-TEST-PC", RandomNumberGenerator.GetBytes(32), default);
        Check(staleDecision.Outcome == ServerLoginOutcome.Denied, "Rowversion anterior no autoriza un login verificado contra datos obsoletos");

        var validBeforeFailure = await Login();
        var beforeFailure = await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.SesionesServidor;");
        await owner.ExecuteAsync("ALTER TABLE sis.Bitacora WITH NOCHECK ADD CONSTRAINT CK_ServerIdentityAuditTest CHECK(Accion<>N'SERVIDOR_LOGIN_CORRECTO');");
        var auditFailed = false;
        try { await service.SignInAsync(Request()); }
        catch (SqlException ex) when (ex.Number == 547) { auditFailed = true; }
        await owner.ExecuteAsync("ALTER TABLE sis.Bitacora DROP CONSTRAINT CK_ServerIdentityAuditTest;");
        Check(auditFailed && await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.SesionesServidor;") == beforeFailure &&
            await service.AuthenticateAsync(validBeforeFailure.AccessToken) is not null,
            "Fallo de auditoría revierte inserción y revocación de sesiones en la misma transacción");

        await owner.ExecuteAsync("INSERT INTO sis.EquiposTrabajo(IdentificadorEquipo,NombreEquipo) VALUES(N'OTHER-TEST-PC',N'Otro equipo sintético');");
        var otherStation = await Login("OTHER-TEST-PC");
        await service.SignOutAsync(validBeforeFailure.AccessToken);
        Check(await service.AuthenticateAsync(otherStation.AccessToken) is not null,
            "Logout de una estación no revoca la sesión de otra estación registrada");

        var countBeforeReapply = await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.SesionesServidor;");
        await DevelopmentDatabase.InstallAsync(database, Path.Combine(root,"database"));
        Check(await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM seg.SesionesServidor;") == countBeforeReapply &&
            await Service().AuthenticateAsync(otherStation.AccessToken) is not null &&
            await Service().AuthenticateAsync(validBeforeFailure.AccessToken) is null,
            "Reaplicar migraciones conserva sesiones y revocaciones previas");
        Check(await owner.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fis.DocumentosDte;") == 0,
            "El bloque de identidad no crea documentos ni estados de Hacienda");
        Console.WriteLine($"{checks} comprobaciones de identidad de servidor SQL correctas.");
    }

    private sealed class LimitedFactory(string database) : IDbConnectionFactory
    {
        public async Task<DbConnection> OpenConnectionAsync(CancellationToken ct = default)
        {
            // Evita que la impersonación de la fixture comparta conexiones con el propietario.
            var connection = new SqlConnection(new SqlConnectionStringBuilder(
                DevelopmentDatabase.ConnectionString(database)) { Pooling = false }.ConnectionString);
            try
            {
                await connection.OpenAsync(ct);
                await connection.ExecuteAsync(new CommandDefinition("EXECUTE AS USER=N'ServerIdentityTests';", cancellationToken: ct));
                await ServerSqlConnectionFactory.VerifyRuntimeIdentityAsync(connection, ct);
                return connection;
            }
            catch { await connection.DisposeAsync(); throw; }
        }
    }
}
