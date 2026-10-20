using System.Data.Common;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;
namespace GoDental.Infrastructure.Repositories;
public sealed class SqlUserRepository(IDbConnectionFactory factory) : IUserRepository
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return (await c.QueryAsync<UserSummary>(new CommandDefinition("""
            SELECT TOP(500) u.IdUsuario Id,u.CodigoEmpleado EmployeeCode,u.Usuario UserName,u.NombreCompleto DisplayName,u.Activo Active,u.DebeCambiarClave MustChangePassword,
            u.UltimoAcceso LastAccess,u.BloqueadoHasta LockedUntil,u.RowVersion,
            ISNULL((SELECT STRING_AGG(r.Nombre,N', ') FROM seg.UsuarioRoles ur JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 WHERE ur.IdUsuario=u.IdUsuario),N'') Roles
            FROM seg.Usuarios u ORDER BY u.NombreCompleto,u.IdUsuario;
            """, cancellationToken: ct))).AsList();
    }
    public async Task<IReadOnlyList<CatalogOption>> RolesAsync(CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return (await c.QueryAsync<CatalogOption>(new CommandDefinition("SELECT IdRol Id,Nombre Name FROM seg.Roles WHERE Activo=1 ORDER BY Nombre", cancellationToken: ct))).AsList();
    }
    public async Task<Result> CreateAsync(string userName, string displayName, int roleId, string hash, int actor, int? equipment, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        if (!await AuthorizeAndLockAsync(c, tx, actor, ct)) return Denied();
        if (!await c.ExecuteScalarAsync<bool>(Cmd("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.Roles WHERE IdRol=@roleId AND Activo=1) THEN 1 ELSE 0 END)", new { roleId }, tx, ct)))
            return Result.Failure("ROLE_CHANGED", "El rol ya no está disponible.");
        try
        {
            var id = await c.ExecuteScalarAsync<int>(Cmd("""
                INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(@userName,@displayName,@hash,1);
                """, new { userName, displayName, hash }, tx, ct));
            await c.ExecuteAsync(Cmd("INSERT INTO seg.UsuarioRoles(IdUsuario,IdRol,AsignadoPor) VALUES(@id,@roleId,@actor); INSERT INTO sis.PreferenciasUsuario(IdUsuario) VALUES(@id);", new { id, roleId, actor }, tx, ct));
            await AuditAsync(c, tx, actor, equipment, id, "CREAR_USUARIO", "Alta con rol y contraseña temporal.", ct);
            await tx.CommitAsync(ct); return Result.Success();
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Result.Failure("DUPLICATE_USER", "Ese nombre de usuario ya existe."); }
    }
    public async Task<Result> SetActiveAsync(UserSummary user, bool active, string reason, int actor, int? equipment, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        if (!await AuthorizeAndLockAsync(c, tx, actor, ct)) return Denied();
        if (user.Id == actor) return Result.Failure("SELF_CHANGE", "No puedes cambiar el estado de tu propia cuenta.");
        if (!active && await c.ExecuteScalarAsync<bool>(Cmd("""
            SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.UsuarioRoles ur JOIN seg.Roles r ON r.IdRol=ur.IdRol WHERE ur.IdUsuario=@Id AND r.Nombre=N'Administrador')
            AND (SELECT COUNT(DISTINCT u.IdUsuario) FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 WHERE u.Activo=1 AND r.Nombre=N'Administrador')<=1 THEN 1 ELSE 0 END);
            """, user, tx, ct))) return Result.Failure("LAST_ADMIN", "Debe permanecer un administrador activo.");
        var count = await c.ExecuteAsync(Cmd("UPDATE seg.Usuarios SET Activo=@active WHERE IdUsuario=@Id AND RowVersion=@RowVersion", new { user.Id, user.RowVersion, active }, tx, ct));
        if (count != 1) return Conflict();
        await AuditAsync(c, tx, actor, equipment, user.Id, active ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO", reason, ct);
        await tx.CommitAsync(ct); return Result.Success();
    }
    public async Task<Result> ResetPasswordAsync(UserSummary user, string hash, string reason, int actor, int? equipment, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        if (!await AuthorizeAndLockAsync(c, tx, actor, ct)) return Denied();
        if (user.Id == actor) return Result.Failure("SELF_CHANGE", "Para tu cuenta, usa cambiar contraseña.");
        var count = await c.ExecuteAsync(Cmd("UPDATE seg.Usuarios SET PasswordHash=@hash,DebeCambiarClave=1,IntentosFallidos=0,BloqueadoHasta=NULL WHERE IdUsuario=@Id AND RowVersion=@RowVersion", new { user.Id, user.RowVersion, hash }, tx, ct));
        if (count != 1) return Conflict();
        await AuditAsync(c, tx, actor, equipment, user.Id, "RESTABLECER_CLAVE", reason, ct);
        await tx.CommitAsync(ct); return Result.Success();
    }
    private static async Task<bool> AuthorizeAndLockAsync(DbConnection c, DbTransaction tx, int actor, CancellationToken ct)
    {
        await c.ExecuteAsync(Cmd("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'GoDental.UserAdministration',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51092,N'La administración de usuarios está ocupada.',1;", null, tx, ct));
        return await c.ExecuteScalarAsync<bool>(Cmd("""
            SELECT CONVERT(bit,CASE WHEN (SELECT COUNT(DISTINCT p.Codigo) FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1 JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE u.IdUsuario=@actor AND u.Activo=1 AND u.DebeCambiarClave=0 AND p.Codigo IN(N'USUARIOS.GESTIONAR',N'SEGURIDAD.ROLES_PERMISOS'))=2 THEN 1 ELSE 0 END);
            """, new { actor }, tx, ct));
    }
    private static Task AuditAsync(DbConnection c, DbTransaction tx, int actor, int? equipment, int id, string action, string reason, CancellationToken ct) => c.ExecuteAsync(Cmd("INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion) VALUES(@actor,@equipment,N'Seguridad',@action,N'Usuarios',@id,@reason);", new { actor, equipment, id, action, reason }, tx, ct));
    private static CommandDefinition Cmd(string sql, object? p, DbTransaction tx, CancellationToken ct) => new(sql, p, tx, cancellationToken: ct);
    private static Result Denied() => Result.Failure("FORBIDDEN", "El permiso de administración ya no está disponible.");
    private static Result Conflict() => Result.Failure("CONCURRENCY_CONFLICT", "Otra estación modificó esta cuenta. Actualiza el listado antes de repetir.");
}
