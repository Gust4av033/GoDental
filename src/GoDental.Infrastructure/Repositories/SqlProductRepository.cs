using System.Data.Common;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Repositories;

public sealed class SqlProductRepository(IDbConnectionFactory factory) : IProductRepository
{
    private const string Columns = """
        p.IdProducto Id,p.SKU Sku,p.Nombre Name,p.Descripcion Description,p.IdCategoria CategoryId,p.IdMarca BrandId,
        p.IdUnidadMedida UnitId,c.Nombre Category,u.Codigo Unit,p.PrecioVentaReferencia Price,p.StockMinimo MinimumStock,
        p.StockMaximo MaximumStock,p.ControlaLote TracksLot,p.ControlaVencimiento TracksExpiry,p.ControlaSerie TracksSerial,p.DiasAlertaVencimiento ExpiryAlertDays,p.Activo Active,p.RowVersion,
        ISNULL((SELECT SUM(ep.StockActual-ep.StockReservado) FROM inv.ExistenciasProducto ep JOIN inv.Almacenes a ON a.IdAlmacen=ep.IdAlmacen AND a.Activo=1 WHERE ep.IdProducto=p.IdProducto),0) AvailableStock
        """;

    public async Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        var filter = """
            WHERE (@CategoryId IS NULL OR p.IdCategoria=@CategoryId) AND (@BrandId IS NULL OR ISNULL(p.IdMarca,0)=@BrandId)
            AND (@LowStockOnly=0 OR ISNULL((SELECT SUM(e.StockActual-e.StockReservado) FROM inv.ExistenciasProducto e JOIN inv.Almacenes a ON a.IdAlmacen=e.IdAlmacen AND a.Activo=1 WHERE e.IdProducto=p.IdProducto),0)<=p.StockMinimo)
            AND (@IncludeInactive=1 OR p.Activo=1) AND (@Search=N'' OR CHARINDEX(@Search,p.SKU)>0 OR CHARINDEX(@Search,p.Nombre)>0
            OR EXISTS(SELECT 1 FROM inv.ProductoCodigosBarra cb WHERE cb.IdProducto=p.IdProducto AND cb.Activo=1 AND cb.Codigo=@Search))
            """;
        var parameters = new { search.Search, search.IncludeInactive, search.CategoryId, search.BrandId, search.LowStockOnly, Offset = checked((search.Page - 1) * search.PageSize), search.PageSize };
        using var grid = await c.QueryMultipleAsync(new CommandDefinition($"""
            SELECT COUNT(*) FROM inv.Productos p {filter};
            SELECT {Columns} FROM inv.Productos p JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria
            JOIN cat.UnidadesMedida u ON u.IdUnidadMedida=p.IdUnidadMedida {filter}
            ORDER BY p.Nombre,p.IdProducto OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, cancellationToken: cancellationToken));
        var total = await grid.ReadSingleAsync<int>();
        return new((await grid.ReadAsync<ProductData>()).AsList(), total);
    }

    public async Task<ProductCatalogs> GetCatalogsAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        using var grid = await c.QueryMultipleAsync(new CommandDefinition("""
            SELECT IdCategoria Id,Nombre Name FROM cat.CategoriasProducto WHERE Activo=1 ORDER BY Nombre;
            SELECT IdMarca Id,Nombre Name FROM cat.Marcas WHERE Activo=1 ORDER BY Nombre;
            SELECT CONVERT(int,IdUnidadMedida) Id,Codigo+N' · '+Nombre Name FROM cat.UnidadesMedida WHERE Activo=1 ORDER BY Nombre;
            """, cancellationToken: cancellationToken));
        return new((await grid.ReadAsync<CatalogOption>()).AsList(), (await grid.ReadAsync<CatalogOption>()).AsList(), (await grid.ReadAsync<CatalogOption>()).AsList());
    }

    public async Task<Result<int>> SaveAsync(ProductData product, int userId, int? workstationId, CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await HasPermissionAsync(c, tx, userId, cancellationToken)) return Result<int>.Failure("FORBIDDEN", "El permiso para gestionar productos ya no está disponible.");
            var catalogsValid = await c.ExecuteScalarAsync<int>(Cmd("""
                SELECT CASE WHEN EXISTS(SELECT 1 FROM cat.CategoriasProducto WITH(HOLDLOCK) WHERE IdCategoria=@CategoryId AND Activo=1)
                AND EXISTS(SELECT 1 FROM cat.UnidadesMedida WITH(HOLDLOCK) WHERE IdUnidadMedida=@UnitId AND Activo=1)
                AND (@BrandId IS NULL OR EXISTS(SELECT 1 FROM cat.Marcas WITH(HOLDLOCK) WHERE IdMarca=@BrandId AND Activo=1)) THEN 1 ELSE 0 END;
                """, product, tx, cancellationToken));
            if (catalogsValid == 0) return Result<int>.Failure("CATALOG_CHANGED", "La categoría, marca o unidad ya no está activa. Recarga los catálogos.");
            ProductData? previous = null;
            if (product.Id > 0)
            {
                previous = await c.QuerySingleOrDefaultAsync<ProductData>(Cmd($"SELECT {Columns} FROM inv.Productos p WITH(UPDLOCK,HOLDLOCK) JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria JOIN cat.UnidadesMedida u ON u.IdUnidadMedida=p.IdUnidadMedida WHERE p.IdProducto=@Id", product, tx, cancellationToken));
                if (previous is null || !previous.RowVersion.SequenceEqual(product.RowVersion)) return Result<int>.Failure("CONCURRENCY_CONFLICT", "Otra estación modificó este producto. Recarga y revisa los cambios antes de guardar.");
                if (previous.Sku != product.Sku && await c.ExecuteScalarAsync<int>(Cmd("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE IdProducto=@Id;", product, tx, cancellationToken)) > 0)
                    return Result<int>.Failure("TRACEABILITY", "El SKU tiene movimientos y debe conservarse para trazabilidad.");
                if ((previous.TracksLot != product.TracksLot || previous.TracksExpiry != product.TracksExpiry || previous.TracksSerial != product.TracksSerial || previous.UnitId != product.UnitId) &&
                    await c.ExecuteScalarAsync<int>(Cmd("""
                        SELECT CASE WHEN EXISTS(SELECT 1 FROM inv.MovimientosInventario WHERE IdProducto=@Id)
                        OR EXISTS(SELECT 1 FROM inv.LotesProducto WHERE IdProducto=@Id)
                        OR EXISTS(SELECT 1 FROM inv.ExistenciasProducto WHERE IdProducto=@Id AND (StockActual<>0 OR StockReservado<>0)) THEN 1 ELSE 0 END;
                        """, product, tx, cancellationToken)) == 1)
                    return Result<int>.Failure("TRACEABILITY", "El producto ya tiene historial. Conserva la unidad y el control de lotes, vencimientos y series.");
            }
            var id = product.Id == 0
                ? await c.ExecuteScalarAsync<int>(Cmd("""
                    INSERT INTO inv.Productos(SKU,Nombre,Descripcion,IdCategoria,IdMarca,IdUnidadMedida,PrecioVentaReferencia,StockMinimo,StockMaximo,ControlaLote,ControlaVencimiento,ControlaSerie,DiasAlertaVencimiento,Activo)
                    OUTPUT INSERTED.IdProducto VALUES(@Sku,@Name,@Description,@CategoryId,@BrandId,@UnitId,@Price,@MinimumStock,@MaximumStock,@TracksLot,@TracksExpiry,@TracksSerial,@ExpiryAlertDays,@Active);
                    """, product, tx, cancellationToken))
                : product.Id;
            if (product.Id != 0)
            {
                var changed = await c.ExecuteAsync(Cmd("""
                    UPDATE inv.Productos SET SKU=@Sku,Nombre=@Name,Descripcion=@Description,IdCategoria=@CategoryId,IdMarca=@BrandId,IdUnidadMedida=@UnitId,
                    PrecioVentaReferencia=@Price,StockMinimo=@MinimumStock,StockMaximo=@MaximumStock,ControlaLote=@TracksLot,ControlaVencimiento=@TracksExpiry,ControlaSerie=@TracksSerial,DiasAlertaVencimiento=@ExpiryAlertDays,Activo=@Active
                    WHERE IdProducto=@Id AND RowVersion=@RowVersion;
                    """, product, tx, cancellationToken));
                if (changed != 1) return Result<int>.Failure("CONCURRENCY_CONFLICT", "El producto cambió. Recarga antes de guardar.");
            }
            var persisted = await c.QuerySingleAsync<ProductData>(Cmd($"SELECT {Columns} FROM inv.Productos p JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria JOIN cat.UnidadesMedida u ON u.IdUnidadMedida=p.IdUnidadMedida WHERE p.IdProducto=@id;", new { id }, tx, cancellationToken));
            await c.ExecuteAsync(Cmd("""
                INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
                VALUES(@userId,@workstationId,N'Inventario',@action,N'Productos',@id,N'Mantenimiento del maestro de productos.',@before,@after);
                """, new { userId, workstationId, id, action = product.Id == 0 ? "CREAR_PRODUCTO" : "EDITAR_PRODUCTO", before = previous is null ? null : JsonSerializer.Serialize(previous), after = JsonSerializer.Serialize(persisted) }, tx, cancellationToken));
            await tx.CommitAsync(cancellationToken);
            return Result<int>.Success(id);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        { return Result<int>.Failure("DUPLICATE_SKU", "Ese SKU ya existe. Elige otro o edita el producto existente."); }
    }

    public async Task<Result<int>> CreateCategoryAsync(string name, int userId, int? workstationId, CancellationToken cancellationToken = default)
    {
        await using var c = await factory.OpenConnectionAsync(cancellationToken);
        await using var tx = await c.BeginTransactionAsync(cancellationToken);
        if (!await HasPermissionAsync(c, tx, userId, cancellationToken)) return Result<int>.Failure("FORBIDDEN", "No tienes permiso para gestionar categorías.");
        try
        {
            var id = await c.ExecuteScalarAsync<int>(Cmd("INSERT INTO cat.CategoriasProducto(Nombre) OUTPUT INSERTED.IdCategoria VALUES(@name);", new { name }, tx, cancellationToken));
            await c.ExecuteAsync(Cmd("INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion) VALUES(@userId,@workstationId,N'Inventario',N'CREAR_CATEGORIA',N'CategoriasProducto',@id,N'Alta de categoría de producto.');", new { userId, workstationId, id }, tx, cancellationToken));
            await tx.CommitAsync(cancellationToken);
            return Result<int>.Success(id);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        { return Result<int>.Failure("DUPLICATE_CATEGORY", "Ya existe esa categoría."); }
    }

    private static CommandDefinition Cmd(string sql, object? parameters, DbTransaction tx, CancellationToken ct) => new(sql, parameters, tx, cancellationToken: ct);
    private static Task<bool> HasPermissionAsync(DbConnection c, DbTransaction tx, int userId, CancellationToken ct) => c.ExecuteScalarAsync<bool>(Cmd("""
        SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
        JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
        JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 AND p.Codigo=N'INVENTARIO.PRODUCTOS_GESTIONAR'
        WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0) THEN 1 ELSE 0 END);
        """, new { userId }, tx, ct));
}

