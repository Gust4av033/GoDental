using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;
namespace GoDental.Infrastructure.Repositories;
public sealed class SqlCategoryRepository(IDbConnectionFactory factory) : ICategoryRepository
{
    private const string Permission = """
        IF NOT EXISTS(SELECT 1 FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
        JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
        JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 AND p.Codigo=N'INVENTARIO.PRODUCTOS_GESTIONAR'
        WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0) THROW 53001,N'No tienes permiso para gestionar categorías.',1;
        """;
    public async Task<IReadOnlyList<CategoryData>> ListAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return (await c.QueryAsync<CategoryData>(new CommandDefinition(Permission+"SELECT IdCategoria Id,Nombre Name,Activo Active FROM cat.CategoriasProducto ORDER BY Nombre;", new { userId }, cancellationToken: ct))).AsList();
    }
    public async Task<Result<int>> SaveAsync(CategoryChange change, int userId, int? workstationId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        try
        {
            await c.ExecuteAsync(new CommandDefinition(Permission, new { userId }, tx, cancellationToken: ct));
            var previous = await c.QuerySingleOrDefaultAsync<CategoryData>(new CommandDefinition("SELECT IdCategoria Id,Nombre Name,Activo Active FROM cat.CategoriasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdCategoria=@Id;", change, tx, cancellationToken: ct));
            if (previous is null || previous.Name != change.ExpectedName || previous.Active != change.ExpectedActive)
                return Result<int>.Failure("CONCURRENCY_CONFLICT", "La categoría cambió. Actualiza antes de editar.");
            if (!change.Active && await c.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS(SELECT 1 FROM inv.Productos WITH(HOLDLOCK) WHERE IdCategoria=@Id AND Activo=1) OR EXISTS(SELECT 1 FROM cat.CategoriasProducto WITH(HOLDLOCK) WHERE IdCategoriaPadre=@Id AND Activo=1) THEN 1 ELSE 0 END;", change, tx, cancellationToken: ct)) == 1)
                return Result<int>.Failure("IN_USE", "Reasigna o desactiva primero los productos y subcategorías activos.");
            await c.ExecuteAsync(new CommandDefinition("UPDATE cat.CategoriasProducto SET Nombre=@Name,Activo=@Active WHERE IdCategoria=@Id;", change, tx, cancellationToken: ct));
            await c.ExecuteAsync(new CommandDefinition("""
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
                VALUES(@userId,@workstationId,N'Inventario',N'EDITAR_CATEGORIA',N'CategoriasProducto',@Id,N'Actualización de nombre o estado de categoría.',@before,@after);
                """, new { userId, workstationId, change.Id, before = JsonSerializer.Serialize(previous), after = JsonSerializer.Serialize(new CategoryData { Id=change.Id,Name=change.Name,Active=change.Active }) }, tx, cancellationToken: ct));
            await tx.CommitAsync(ct); return Result<int>.Success(change.Id);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Result<int>.Failure("DUPLICATE_CATEGORY", "Ya existe una categoría con ese nombre bajo el mismo padre."); }
        catch (SqlException ex) when (ex.Number == 53001) { return Result<int>.Failure("FORBIDDEN", ex.Message); }
    }
}
