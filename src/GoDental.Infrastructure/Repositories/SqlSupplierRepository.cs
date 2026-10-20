using System.Data;
using System.Data.Common;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Repositories;

public sealed class SqlSupplierRepository(IDbConnectionFactory factory) : ISupplierRepository
{
    private const string Authorization = """
        DECLARE @Permissions TABLE(Code nvarchar(100));
        INSERT INTO @Permissions SELECT DISTINCT p.Codigo FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
        JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
        JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
        WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0;
        IF NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'COMPRAS.VER')
            OR (@Manage=1 AND NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'PROVEEDORES.GESTIONAR'))
            THROW 53201,N'El permiso para consultar o gestionar proveedores ya no está disponible.',1;
        DECLARE @CanSeeCosts bit=CASE WHEN EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'PROVEEDORES.GESTIONAR') THEN 1 ELSE 0 END;
        """;
    private const string Columns = "IdProveedor Id,Codigo Code,Nombre Name,Pais Country,Direccion Address,Email,Telefono Phone,SitioWeb Website,Activo Active,RowVersion";
    private const string ContactColumns = "IdContacto Id,Nombre Name,Cargo Position,Email,Telefono Phone,EsPrincipal [Primary],Activo Active";

    public async Task<SupplierPage> SearchAsync(SupplierSearch search, int userId, CancellationToken ct = default)
    {
        const string where = " WHERE (@IncludeInactive=1 OR Activo=1) AND (@Search=N'' OR CHARINDEX(@Search,Codigo)>0 OR CHARINDEX(@Search,Nombre)>0 OR CHARINDEX(@Search,Pais)>0) ";
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await connection.QueryMultipleAsync(Cmd(Authorization + "SELECT COUNT(*) FROM cmp.Proveedores" + where + ";" +
                $"SELECT {Columns} FROM cmp.Proveedores" + where + "ORDER BY Nombre,IdProveedor OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
                new { userId, Manage = false, search.Search, search.IncludeInactive, Offset = checked((search.Page - 1) * search.PageSize), search.PageSize }, tx, ct));
            var total = await rows.ReadSingleAsync<int>();
            var items = (await rows.ReadAsync<SupplierData>()).AsList();
            await tx.CommitAsync(ct); return new(items, total);
        }
        catch (SqlException ex) when (ex.Number == 53201) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }

    public async Task<SupplierSnapshot?> ReadAsync(int supplierId, int userId, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await connection.QueryMultipleAsync(Cmd(Authorization + $"SELECT {Columns} FROM cmp.Proveedores WHERE IdProveedor=@supplierId;" +
                $"SELECT {ContactColumns} FROM cmp.ProveedorContactos WHERE IdProveedor=@supplierId ORDER BY Activo DESC,EsPrincipal DESC,Nombre,IdContacto;" + """
                SELECT pp.IdProducto ProductId,p.SKU LocalSku,p.Nombre Product,pp.SkuProveedor SupplierSku,
                    CASE WHEN @CanSeeCosts=1 THEN pp.CostoUltimo END LastCost,pp.IdMoneda CurrencyId,m.CodigoISO Currency,
                    pp.CantidadMinima MinimumQuantity,pp.LeadTimeDias LeadTimeDays,pp.EsPreferido Preferred,pp.Activo Active
                FROM cmp.ProveedorProductos pp JOIN inv.Productos p ON p.IdProducto=pp.IdProducto
                LEFT JOIN cat.Monedas m ON m.IdMoneda=pp.IdMoneda WHERE pp.IdProveedor=@supplierId ORDER BY p.Nombre,p.IdProducto;
                """, new { supplierId, userId, Manage = false }, tx, ct));
            var supplier = await rows.ReadSingleOrDefaultAsync<SupplierData>();
            var contacts = (await rows.ReadAsync<SupplierContactData>()).AsList();
            var products = (await rows.ReadAsync<SupplierProductData>()).AsList();
            await tx.CommitAsync(ct);
            return supplier is null ? null : new(supplier, contacts, products);
        }
        catch (SqlException ex) when (ex.Number == 53201) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }

    public async Task<IReadOnlyList<CatalogOption>> CurrenciesAsync(int userId, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        try
        {
            return (await connection.QueryAsync<CatalogOption>(new CommandDefinition(Authorization +
                "SELECT IdMoneda Id,CONCAT(CodigoISO,N' · ',Nombre) Name FROM cat.Monedas WHERE Activo=1 ORDER BY CodigoISO;",
                new { userId, Manage = false }, cancellationToken: ct))).AsList();
        }
        catch (SqlException ex) when (ex.Number == 53201) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }

    public Task<Result<int>> SaveAsync(SupplierChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Supplier;
        return MutateAsync(s.Id, s.RowVersion, false, userId, ct, async (c, tx, previous) =>
        {
            if (previous is not null && previous.Code != s.Code && await c.ExecuteScalarAsync<bool>(Cmd(
                "SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.OrdenesCompra WITH(HOLDLOCK) WHERE IdProveedor=@Id) THEN 1 ELSE 0 END);", s, tx, ct)))
                return Result<int>.Failure("IN_USE", "El proveedor tiene órdenes; conserva su código para mantener trazabilidad.");
            var inserting = s.Id == 0 && string.IsNullOrWhiteSpace(s.Code)
                ? s with { Code = await GoDental.Infrastructure.Data.MasterCode.NextAsync(c, tx, "MAESTRO_PROVEEDOR", ct) } : s;
            var id = s.Id == 0 ? await c.ExecuteScalarAsync<int>(Cmd("""
                INSERT INTO cmp.Proveedores(Codigo,Nombre,Pais,Direccion,Email,Telefono,SitioWeb,Activo)
                OUTPUT INSERTED.IdProveedor VALUES(@Code,@Name,@Country,@Address,@Email,@Phone,@Website,@Active);
                """, inserting, tx, ct)) : s.Id;
            if (s.Id != 0) await c.ExecuteAsync(Cmd("""
                UPDATE cmp.Proveedores SET Codigo=@Code,Nombre=@Name,Pais=@Country,Direccion=@Address,Email=@Email,Telefono=@Phone,SitioWeb=@Website,Activo=@Active
                WHERE IdProveedor=@Id;
                """, s, tx, ct));
            var persisted = await c.QuerySingleAsync<SupplierData>(Cmd($"SELECT {Columns} FROM cmp.Proveedores WHERE IdProveedor=@id;", new { id }, tx, ct));
            await AuditAsync(c, tx, id, userId, workstationId, s.Id == 0 ? "CREAR_PROVEEDOR" : "EDITAR_PROVEEDOR", request.Reason, previous, persisted, ct);
            return Result<int>.Success(id);
        });
    }

    public Task<Result<int>> SaveContactAsync(SupplierContactChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        var contact = request.Contact;
        return MutateAsync(request.SupplierId, request.ExpectedSupplierVersion, true, userId, ct, async (c, tx, _) =>
        {
            var previous = contact.Id == 0 ? null : await c.QuerySingleOrDefaultAsync<SupplierContactData>(Cmd(
                $"SELECT {ContactColumns} FROM cmp.ProveedorContactos WHERE IdProveedor=@SupplierId AND IdContacto=@Id;",
                new { request.SupplierId, contact.Id }, tx, ct));
            if (contact.Id != 0 && previous is null) return Result<int>.Failure("VALIDATION", "El contacto no pertenece a este proveedor.");
            if (contact.Primary && contact.Active && await c.ExecuteScalarAsync<bool>(Cmd("""
                SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.ProveedorContactos WHERE IdProveedor=@SupplierId
                    AND Activo=1 AND EsPrincipal=1 AND IdContacto<>@Id) THEN 1 ELSE 0 END);
                """, new { request.SupplierId, contact.Id }, tx, ct)))
                return Result<int>.Failure("PRIMARY_CONTACT_EXISTS", "Desmarca el contacto principal actual antes de elegir otro.");
            var parameters = new { request.SupplierId, contact.Id, contact.Name, contact.Position, contact.Email, contact.Phone, contact.Primary, contact.Active };
            var id = contact.Id == 0 ? await c.ExecuteScalarAsync<int>(Cmd("""
                INSERT INTO cmp.ProveedorContactos(IdProveedor,Nombre,Cargo,Email,Telefono,EsPrincipal,Activo)
                OUTPUT INSERTED.IdContacto VALUES(@SupplierId,@Name,@Position,@Email,@Phone,@Primary,@Active);
                """, parameters, tx, ct)) : contact.Id;
            if (contact.Id != 0) await c.ExecuteAsync(Cmd("""
                UPDATE cmp.ProveedorContactos SET Nombre=@Name,Cargo=@Position,Email=@Email,Telefono=@Phone,EsPrincipal=@Primary,Activo=@Active
                WHERE IdContacto=@Id AND IdProveedor=@SupplierId;
                """, parameters, tx, ct));
            await TouchAsync(c, tx, request.SupplierId, ct);
            await AuditAsync(c, tx, request.SupplierId, userId, workstationId, contact.Id == 0 ? "CREAR_CONTACTO" : "EDITAR_CONTACTO", request.Reason,
                previous, contact with { Id = id }, ct);
            return Result<int>.Success(request.SupplierId);
        });
    }

    public Task<Result<int>> SaveProductAsync(SupplierProductChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (SupplierValidator.Validate(request) is { } error) return Invalid(error);
        var product = request.Product;
        return MutateAsync(request.SupplierId, request.ExpectedSupplierVersion, true, userId, ct, async (c, tx, _) =>
        {
            if (!await c.ExecuteScalarAsync<bool>(Cmd("""
                SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.Productos WITH(HOLDLOCK) WHERE IdProducto=@ProductId AND (@Active=0 OR Activo=1))
                    AND (@CurrencyId IS NULL OR EXISTS(SELECT 1 FROM cat.Monedas WITH(HOLDLOCK) WHERE IdMoneda=@CurrencyId AND (@Active=0 OR Activo=1))) THEN 1 ELSE 0 END);
                """, product, tx, ct))) return Result<int>.Failure("VALIDATION", "El producto o la moneda no existen o ya no están activos.");
            var previous = await c.QuerySingleOrDefaultAsync<SupplierProductData>(Cmd("""
                SELECT IdProducto ProductId,SkuProveedor SupplierSku,CostoUltimo LastCost,IdMoneda CurrencyId,CantidadMinima MinimumQuantity,
                    LeadTimeDias LeadTimeDays,EsPreferido Preferred,Activo Active FROM cmp.ProveedorProductos WHERE IdProveedor=@SupplierId AND IdProducto=@ProductId;
                """, new { request.SupplierId, product.ProductId }, tx, ct));
            var parameters = new { request.SupplierId, product.ProductId, product.SupplierSku, product.LastCost, product.CurrencyId,
                product.MinimumQuantity, product.LeadTimeDays, product.Preferred, product.Active };
            await c.ExecuteAsync(Cmd(previous is null ? """
                INSERT INTO cmp.ProveedorProductos(IdProveedor,IdProducto,SkuProveedor,CostoUltimo,IdMoneda,CantidadMinima,LeadTimeDias,EsPreferido,Activo)
                VALUES(@SupplierId,@ProductId,@SupplierSku,@LastCost,@CurrencyId,@MinimumQuantity,@LeadTimeDays,@Preferred,@Active);
                """ : """
                UPDATE cmp.ProveedorProductos SET SkuProveedor=@SupplierSku,CostoUltimo=@LastCost,IdMoneda=@CurrencyId,CantidadMinima=@MinimumQuantity,
                    LeadTimeDias=@LeadTimeDays,EsPreferido=@Preferred,Activo=@Active WHERE IdProveedor=@SupplierId AND IdProducto=@ProductId;
                """, parameters, tx, ct));
            await TouchAsync(c, tx, request.SupplierId, ct);
            await AuditAsync(c, tx, request.SupplierId, userId, workstationId, "GUARDAR_PRODUCTO_PROVEEDOR", request.Reason, previous,
                new { product.ProductId, product.SupplierSku, product.LastCost, product.CurrencyId, product.MinimumQuantity, product.LeadTimeDays, product.Preferred, product.Active }, ct);
            return Result<int>.Success(request.SupplierId);
        });
    }

    private async Task<Result<int>> MutateAsync(int supplierId, byte[] expectedVersion, bool requireActive, int userId, CancellationToken ct,
        Func<DbConnection, DbTransaction, SupplierData?, Task<Result<int>>> write)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await connection.ExecuteAsync(Cmd(Authorization, new { userId, Manage = true }, tx, ct));
            SupplierData? previous = null;
            if (supplierId != 0)
            {
                previous = await connection.QuerySingleOrDefaultAsync<SupplierData>(Cmd(
                    $"SELECT {Columns} FROM cmp.Proveedores WITH(UPDLOCK,HOLDLOCK) WHERE IdProveedor=@supplierId;", new { supplierId }, tx, ct));
                if (previous is null || !previous.RowVersion.SequenceEqual(expectedVersion))
                    return Result<int>.Failure("CONCURRENCY_CONFLICT", "Otra estación modificó este proveedor o sus datos relacionados. Recarga antes de guardar.");
                if (requireActive && !previous.Active) return Result<int>.Failure("INVALID_STATE", "Reactiva el proveedor antes de modificar contactos o productos.");
            }
            var result = await write(connection, tx, previous);
            if (result.IsSuccess) await tx.CommitAsync(ct);
            return result;
        }
        catch (SqlException ex) when (ex.Number == 53201) { return Result<int>.Failure("FORBIDDEN", ex.Message); }
        catch (SqlException ex) when (ex.Number is 53501 or 53502) { return Result<int>.Failure("CODE_UNAVAILABLE", ex.Message); }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Result<int>.Failure("DUPLICATE_CODE", "Ya existe un proveedor con ese código."); }
        catch (SqlException ex) when (ex.Number == 1205) { return Result<int>.Failure("CONCURRENCY_CONFLICT", "Otra operación utilizó estos datos. Recarga y revisa antes de repetir."); }
    }

    private static CommandDefinition Cmd(string sql, object? parameters, DbTransaction tx, CancellationToken ct) => new(sql, parameters, tx, cancellationToken: ct);
    private static Task TouchAsync(DbConnection connection, DbTransaction tx, int supplierId, CancellationToken ct) => connection.ExecuteAsync(Cmd(
        "UPDATE cmp.Proveedores SET Nombre=Nombre WHERE IdProveedor=@supplierId;", new { supplierId }, tx, ct));
    private static Task AuditAsync(DbConnection connection, DbTransaction tx, int supplierId, int userId, int? workstationId,
        string action, string reason, object? before, object after, CancellationToken ct) => connection.ExecuteAsync(Cmd("""
            INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
            VALUES(@userId,@workstationId,N'Compras',@action,N'Proveedores',@supplierId,@reason,@beforeJson,@afterJson);
            """, new { supplierId, userId, workstationId, action, reason, beforeJson = before is null ? null : JsonSerializer.Serialize(before), afterJson = JsonSerializer.Serialize(after) }, tx, ct));
    private static Task<Result<int>> Invalid(string error) => Task.FromResult(Result<int>.Failure("VALIDATION", error));
}
