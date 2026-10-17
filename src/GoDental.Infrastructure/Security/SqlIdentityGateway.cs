using System.Data.Common;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Infrastructure.Security;

public sealed class SqlIdentityGateway(IDbConnectionFactory factory, IPasswordHasher hasher) : IIdentityGateway
{
    private const string InvalidLogin = "Usuario o contraseña incorrectos, o acceso temporalmente bloqueado.";

    public async Task<bool> NeedsAdministratorAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        return await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM seg.Usuarios;", cancellationToken: cancellationToken)) == 0;
    }

    public async Task<Result<UserIdentity>> CreateAdministratorAsync(string userName, string displayName, string password, string workstation, CancellationToken cancellationToken = default)
    {
        userName = userName.Trim(); displayName = displayName.Trim();
        var validation = IdentityValidator.ValidateUser(userName, displayName) ?? IdentityValidator.ValidatePassword(userName, password);
        if (validation is not null) return Result<UserIdentity>.Failure("VALIDATION", validation);
        var hash = await Task.Run(() => hasher.Hash(password), cancellationToken);
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        await LockAsync(c, tx, "GoDental.FirstAdministrator", cancellationToken);
        if (await c.ExecuteScalarAsync<int>(Cmd("SELECT CASE WHEN IS_SRVROLEMEMBER('sysadmin')=1 OR IS_MEMBER('db_owner')=1 THEN 1 ELSE 0 END", null, tx, cancellationToken)) != 1)
            return Result<UserIdentity>.Failure("SETUP_OWNER_REQUIRED", "La creación inicial requiere al responsable de la instalación de SQL Server.");
        if (await c.ExecuteScalarAsync<int>(Cmd("SELECT COUNT(*) FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK);", null, tx, cancellationToken)) != 0)
            return Result<UserIdentity>.Failure("SETUP_COMPLETED", "El administrador inicial ya fue creado. Inicia sesión.");
        var roleId = await c.ExecuteScalarAsync<int?>(Cmd("SELECT IdRol FROM seg.Roles WHERE Nombre=N'Administrador' AND Activo=1", null, tx, cancellationToken));
        if (roleId is null) return Result<UserIdentity>.Failure("SEED_REQUIRED", "Faltan los roles iniciales. Revisa la instalación de la base.");
        var id = await c.ExecuteScalarAsync<int>(Cmd("""
            INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,DebeCambiarClave,UltimoAcceso)
            OUTPUT INSERTED.IdUsuario VALUES(@userName,@displayName,@hash,0,SYSDATETIMEOFFSET());
            """, new { userName, displayName, hash }, tx, cancellationToken));
        await c.ExecuteAsync(Cmd("""
            INSERT INTO seg.UsuarioRoles(IdUsuario,IdRol,AsignadoPor) VALUES(@id,@roleId,@id);
            INSERT INTO sis.PreferenciasUsuario(IdUsuario) VALUES(@id);
            """, new { id, roleId }, tx, cancellationToken));
        var equipment = await RegisterWorkstationAsync(c, tx, workstation, cancellationToken);
        await AuditAsync(c, tx, id, equipment, "CREAR_ADMINISTRADOR_INICIAL", cancellationToken);
        var identity = await IdentityAsync(c, tx, id, equipment, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result<UserIdentity>.Success(identity);
    }

    public async Task<Result<UserIdentity>> SignInAsync(string userName, string password, string workstation, CancellationToken cancellationToken = default)
    {
        userName = userName.Trim();
        if (userName.Length is < 1 or > 60 || password.Length is < 1 or > 128)
            return Result<UserIdentity>.Failure("INVALID_CREDENTIALS", InvalidLogin);
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        await LockAsync(c, tx, "GoDental.Login:" + userName.ToUpperInvariant(), cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<LoginRow>(Cmd("""
            SELECT IdUsuario Id, Usuario UserName, PasswordHash, Activo Active, DebeCambiarClave MustChangePassword,
                CASE WHEN BloqueadoHasta>SYSDATETIMEOFFSET() THEN 1 ELSE 0 END Locked
            FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Usuario=@userName;
            """, new { userName }, tx, cancellationToken));
        var verified = await Task.Run(() => hasher.Verify(password, row?.PasswordHash ?? Pbkdf2PasswordHasher.DummyHash), cancellationToken);
        if (row is null || !verified || !row.Active || row.Locked)
        {
            if (row is { Active: true, Locked: false })
            {
                await c.ExecuteAsync(Cmd("""
                    DECLARE @MaxAttempts int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.MaxIntentos'),5);
                    DECLARE @Minutes int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.BloqueoMinutos'),15);
                    IF @MaxAttempts NOT BETWEEN 1 AND 100 SET @MaxAttempts=5;
                    IF @Minutes NOT BETWEEN 1 AND 1440 SET @Minutes=15;
                    UPDATE seg.Usuarios SET IntentosFallidos=CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END,
                      BloqueadoHasta=CASE WHEN (CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END)>=@MaxAttempts
                        THEN DATEADD(minute,@Minutes,SYSDATETIMEOFFSET()) ELSE NULL END
                    WHERE IdUsuario=@id;
                    """, new { id = row.Id }, tx, cancellationToken));
            }
            await AuditAsync(c, tx, row?.Id, null, "LOGIN_FALLIDO", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<UserIdentity>.Failure("INVALID_CREDENTIALS", InvalidLogin);
        }
        var updatedHash = row.PasswordHash.StartsWith($"PBKDF2-SHA256${Pbkdf2PasswordHasher.Iterations}$", StringComparison.Ordinal)
            ? row.PasswordHash : await Task.Run(() => hasher.Hash(password), cancellationToken);
        var equipment = await RegisterWorkstationAsync(c, tx, workstation, cancellationToken);
        await c.ExecuteAsync(Cmd("UPDATE seg.Usuarios SET IntentosFallidos=0,BloqueadoHasta=NULL,UltimoAcceso=SYSDATETIMEOFFSET(),PasswordHash=@updatedHash WHERE IdUsuario=@id;", new { id = row.Id, updatedHash }, tx, cancellationToken));
        await AuditAsync(c, tx, row.Id, equipment, "LOGIN_CORRECTO", cancellationToken);
        var identity = await IdentityAsync(c, tx, row.Id, equipment, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result<UserIdentity>.Success(identity);
    }

    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (currentPassword.Length is < 1 or > 128) return Result.Failure("INVALID_CREDENTIALS", InvalidLogin);
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        var row = await c.QuerySingleOrDefaultAsync<LoginRow>(Cmd("""
            SELECT IdUsuario Id,Usuario UserName,PasswordHash FROM seg.Usuarios WITH(UPDLOCK,HOLDLOCK)
            WHERE IdUsuario=@userId AND Activo=1 AND (BloqueadoHasta IS NULL OR BloqueadoHasta<=SYSDATETIMEOFFSET());
            """, new { userId }, tx, cancellationToken));
        if (row is null || !await Task.Run(() => hasher.Verify(currentPassword, row.PasswordHash), cancellationToken))
        {
            if (row is not null)
                await c.ExecuteAsync(Cmd("""
                    DECLARE @Max int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.MaxIntentos'),5);
                    DECLARE @Minutes int=ISNULL((SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WHERE Clave=N'Seguridad.BloqueoMinutos'),15);
                    IF @Max NOT BETWEEN 1 AND 100 SET @Max=5;
                    IF @Minutes NOT BETWEEN 1 AND 1440 SET @Minutes=15;
                    UPDATE seg.Usuarios SET IntentosFallidos=CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END,
                    BloqueadoHasta=CASE WHEN (CASE WHEN BloqueadoHasta IS NOT NULL THEN 1 ELSE IntentosFallidos+1 END)>=@Max THEN DATEADD(minute,@Minutes,SYSDATETIMEOFFSET()) ELSE NULL END
                    WHERE IdUsuario=@userId;
                    """, new { userId }, tx, cancellationToken));
            await AuditAsync(c, tx, row?.Id, null, "CAMBIO_CLAVE_FALLIDO", cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Failure("INVALID_CREDENTIALS", InvalidLogin);
        }
        if (IdentityValidator.ValidatePassword(row.UserName, newPassword) is { } error) return Result.Failure("VALIDATION", error);
        if (newPassword == currentPassword) return Result.Failure("VALIDATION", "Elige una contraseña diferente de la anterior.");
        var hash = await Task.Run(() => hasher.Hash(newPassword), cancellationToken);
        await c.ExecuteAsync(Cmd("UPDATE seg.Usuarios SET PasswordHash=@hash,DebeCambiarClave=0,IntentosFallidos=0,BloqueadoHasta=NULL WHERE IdUsuario=@userId;", new { hash, userId }, tx, cancellationToken));
        await AuditAsync(c, tx, userId, null, "CAMBIAR_CLAVE", cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task SignOutAsync(int userId, int? workstationId, CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        await AuditAsync(c, tx, userId, workstationId, "CERRAR_SESION", cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static CommandDefinition Cmd(string sql, object? parameters, DbTransaction tx, CancellationToken ct) => new(sql, parameters, tx, commandTimeout: 15, cancellationToken: ct);
    private static Task LockAsync(DbConnection c, DbTransaction tx, string resource, CancellationToken ct) => c.ExecuteAsync(Cmd("""
        DECLARE @r int;
        EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
        IF @r<0 THROW 51090,N'No se pudo reservar la operación de seguridad. Vuelva a intentarlo.',1;
        """, new { resource }, tx, ct));

    private static async Task<int> RegisterWorkstationAsync(DbConnection c, DbTransaction tx, string workstation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workstation) || workstation.Length > 120) throw new ArgumentException("Estación inválida.");
        return await c.ExecuteScalarAsync<int>(Cmd("""
            DECLARE @id int=(SELECT IdEquipo FROM sis.EquiposTrabajo WITH(UPDLOCK,HOLDLOCK) WHERE IdentificadorEquipo=@workstation);
            IF @id IS NULL BEGIN
              INSERT INTO sis.EquiposTrabajo(IdentificadorEquipo,NombreEquipo,UltimaConexion) VALUES(@workstation,@workstation,SYSDATETIMEOFFSET());
              SET @id=SCOPE_IDENTITY();
            END
            IF EXISTS(SELECT 1 FROM sis.EquiposTrabajo WHERE IdEquipo=@id AND Activo=0) THROW 51091,N'Esta estación no está habilitada.',1;
            UPDATE sis.EquiposTrabajo SET UltimaConexion=SYSDATETIMEOFFSET() WHERE IdEquipo=@id;
            SELECT @id;
            """, new { workstation }, tx, ct));
    }

    private static async Task<UserIdentity> IdentityAsync(DbConnection c, DbTransaction tx, int id, int equipment, CancellationToken ct)
    {
        var user = await c.QuerySingleAsync<IdentityRow>(Cmd("SELECT Usuario UserName,NombreCompleto DisplayName,DebeCambiarClave MustChangePassword FROM seg.Usuarios WHERE IdUsuario=@id;", new { id }, tx, ct));
        var roles = (await c.QueryAsync<string>(Cmd("SELECT r.Nombre FROM seg.UsuarioRoles ur JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 WHERE ur.IdUsuario=@id", new { id }, tx, ct))).ToArray();
        var permissions = (await c.QueryAsync<string>(Cmd("""
            SELECT DISTINCT p.Codigo FROM seg.UsuarioRoles ur JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1
            JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1 JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 WHERE ur.IdUsuario=@id;
            """, new { id }, tx, ct))).ToArray();
        var scope = await c.ExecuteScalarAsync<string>(Cmd("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))+N'|'+DB_NAME();", null, tx, ct));
        return new(id, user.UserName, user.DisplayName, equipment, roles, permissions, user.MustChangePassword, scope ?? "");
    }

    private static Task AuditAsync(DbConnection c, DbTransaction tx, int? id, int? equipment, string action, CancellationToken ct) =>
        c.ExecuteAsync(Cmd("INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion) VALUES(@id,@equipment,N'Seguridad',@action,N'Usuarios',@id,@action);", new { id, equipment, action }, tx, ct));

    private sealed class LoginRow
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public bool Active { get; set; }
        public bool Locked { get; set; }
    }
    private sealed class IdentityRow
    {
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool MustChangePassword { get; set; }
    }
}
