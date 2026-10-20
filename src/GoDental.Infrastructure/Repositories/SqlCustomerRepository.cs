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

public sealed class SqlCustomerRepository(IDbConnectionFactory factory) : ICustomerRepository
{
    private const string Authorization = """
        DECLARE @Permissions TABLE(Code nvarchar(100));
        INSERT INTO @Permissions SELECT DISTINCT p.Codigo FROM seg.Usuarios u
        JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1
        JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1 JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
        WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0;
        IF NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'CLIENTES.VER')
          OR (@RequiredPermission IS NOT NULL AND NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=@RequiredPermission))
          OR (@History=1 AND NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'VENTAS.VER'))
            THROW 53301,N'El permiso para esta operación de clientes ya no está disponible.',1;
        """;
    private const string Columns = "c.IdCliente Id,c.Codigo Code,c.IdTipoCliente TypeId,t.Nombre [Type],c.NombreRazonSocial Name,c.NombreComercial CommercialName,c.NIT Nit,c.NRC Nrc,c.DUI Dui,c.Giro BusinessActivity,c.Telefono Phone,c.WhatsApp,c.Email,c.IdListaPrecio PriceListId,l.Nombre PriceList,c.Activo Active,c.RowVersion";
    private const string Joins = " JOIN cat.TiposCliente t ON t.IdTipoCliente=c.IdTipoCliente LEFT JOIN ven.ListasPrecio l ON l.IdListaPrecio=c.IdListaPrecio ";
    private const string ContactColumns = "IdContacto Id,Nombre Name,Cargo Position,Telefono Phone,WhatsApp,Email,EsPrincipal [Primary],Activo Active";
    private const string AddressColumns = "IdDireccion Id,Tipo [Type],Departamento Department,Municipio Municipality,Direccion Address,Referencia Reference,EsPrincipal [Primary],Activo Active";
    private const string CreditColumns = "PermiteCredito Enabled,LimiteCredito [Limit],DiasCredito Days,IntervaloAvisoDias NoticeDays,BloqueadoCredito Blocked,MotivoBloqueoCredito BlockReason";

    public async Task<CustomerPage> SearchAsync(CustomerSearch search, int userId, CancellationToken ct = default)
    {
        const string where = " WHERE (@IncludeInactive=1 OR c.Activo=1) AND (@TypeId IS NULL OR c.IdTipoCliente=@TypeId) AND (@Search=N'' OR CHARINDEX(@Search,c.Codigo)>0 OR CHARINDEX(@Search,c.NombreRazonSocial)>0 OR CHARINDEX(@Search,c.NombreComercial)>0 OR CHARINDEX(@Search,c.NIT)>0 OR CHARINDEX(@Search,c.NRC)>0 OR CHARINDEX(@Search,c.Telefono)>0) ";
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await c.QueryMultipleAsync(Cmd(Authorization + "SELECT COUNT(*) FROM cli.Clientes c" + where + ";SELECT " + Columns + " FROM cli.Clientes c" + Joins + where + " ORDER BY c.NombreRazonSocial,c.IdCliente OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
                new { userId, RequiredPermission = (string?)null, History = false, search.Search, search.IncludeInactive, search.TypeId, Offset = checked((search.Page - 1) * search.PageSize), search.PageSize }, tx, ct));
            var total = await rows.ReadSingleAsync<int>(); var items = (await rows.ReadAsync<CustomerData>()).AsList();
            await tx.CommitAsync(ct); return new(items, total);
        }
        catch (SqlException ex) when (ex.Number == 53301) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }
    public async Task<CustomerCatalogs> CatalogsAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        try
        {
            using var rows = await c.QueryMultipleAsync(new CommandDefinition(Authorization + "SELECT IdTipoCliente Id,Nombre Name FROM cat.TiposCliente WHERE Activo=1 ORDER BY Nombre;SELECT IdListaPrecio Id,Nombre Name FROM ven.ListasPrecio WHERE Activo=1 ORDER BY Nombre;",
                new { userId, RequiredPermission = (string?)null, History = false }, cancellationToken: ct));
            return new((await rows.ReadAsync<CatalogOption>()).AsList(), (await rows.ReadAsync<CatalogOption>()).AsList());
        }
        catch (SqlException ex) when (ex.Number == 53301) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }
    public async Task<CustomerSnapshot?> ReadAsync(long id, int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await c.QueryMultipleAsync(Cmd(Authorization + $"SELECT {Columns} FROM cli.Clientes c{Joins} WHERE c.IdCliente=@id;SELECT {CreditColumns} FROM cli.Clientes WHERE IdCliente=@id;SELECT {ContactColumns} FROM cli.ClienteContactos WHERE IdCliente=@id ORDER BY Activo DESC,EsPrincipal DESC,Nombre,IdContacto;SELECT {AddressColumns} FROM cli.ClienteDirecciones WHERE IdCliente=@id ORDER BY Activo DESC,Tipo,EsPrincipal DESC,IdDireccion;",
                new { id, userId, RequiredPermission = (string?)null, History = false }, tx, ct));
            var customer = await rows.ReadSingleOrDefaultAsync<CustomerData>(); var credit = await rows.ReadSingleOrDefaultAsync<CustomerCreditData>();
            var contacts = (await rows.ReadAsync<CustomerContactData>()).AsList(); var addresses = (await rows.ReadAsync<CustomerAddressData>()).AsList();
            await tx.CommitAsync(ct); return customer is null ? null : new(customer, credit!, contacts, addresses);
        }
        catch (SqlException ex) when (ex.Number == 53301) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }
    public Task<Result<long>> SaveAsync(CustomerChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Customer;
        return MutateAsync(s.Id, s.RowVersion, s.Id == 0 ? "CLIENTES.CREAR" : "CLIENTES.EDITAR", false, userId, ct, async (c, tx, previous) =>
        {
            if (!await c.ExecuteScalarAsync<bool>(Cmd("""
                SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cat.TiposCliente WITH(HOLDLOCK) WHERE IdTipoCliente=@TypeId AND (Activo=1 OR IdTipoCliente=@OldType))
                  AND (@PriceListId IS NULL OR EXISTS(SELECT 1 FROM ven.ListasPrecio WITH(HOLDLOCK) WHERE IdListaPrecio=@PriceListId AND (Activo=1 OR IdListaPrecio=@OldList))) THEN 1 ELSE 0 END);
                """, new { s.TypeId, s.PriceListId, OldType = previous?.TypeId, OldList = previous?.PriceListId }, tx, ct))) return Result<long>.Failure("VALIDATION", "El tipo de cliente o la lista no están disponibles.");
            if (previous is not null && previous.Code != s.Code && await c.ExecuteScalarAsync<bool>(Cmd("""
                SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.Ventas WITH(HOLDLOCK) WHERE IdCliente=@Id)
                  OR EXISTS(SELECT 1 FROM ven.Cotizaciones WITH(HOLDLOCK) WHERE IdCliente=@Id)
                  OR EXISTS(SELECT 1 FROM ven.CuentasPorCobrar WITH(HOLDLOCK) WHERE IdCliente=@Id) THEN 1 ELSE 0 END);
                """, s, tx, ct))) return Result<long>.Failure("IN_USE", "Conserva el código del cliente porque tiene historial comercial.");
            var inserting = s.Id == 0 && string.IsNullOrWhiteSpace(s.Code)
                ? s with { Code = await GoDental.Infrastructure.Data.MasterCode.NextAsync(c, tx, "MAESTRO_CLIENTE", ct) } : s;
            var id = s.Id == 0 ? await c.ExecuteScalarAsync<long>(Cmd("""
                INSERT INTO cli.Clientes(Codigo,IdTipoCliente,NombreRazonSocial,NombreComercial,NIT,NRC,DUI,Giro,Telefono,WhatsApp,Email,IdListaPrecio,Activo)
                OUTPUT INSERTED.IdCliente VALUES(@Code,@TypeId,@Name,@CommercialName,@Nit,@Nrc,@Dui,@BusinessActivity,@Phone,@WhatsApp,@Email,@PriceListId,@Active);
                """, inserting, tx, ct)) : s.Id;
            if (s.Id != 0) await c.ExecuteAsync(Cmd("""
                UPDATE cli.Clientes SET Codigo=@Code,IdTipoCliente=@TypeId,NombreRazonSocial=@Name,NombreComercial=@CommercialName,NIT=@Nit,NRC=@Nrc,DUI=@Dui,Giro=@BusinessActivity,Telefono=@Phone,WhatsApp=@WhatsApp,Email=@Email,IdListaPrecio=@PriceListId,Activo=@Active WHERE IdCliente=@Id;
                """, s, tx, ct));
            var persisted = await c.QuerySingleAsync<CustomerData>(Cmd($"SELECT {Columns} FROM cli.Clientes c{Joins} WHERE c.IdCliente=@id;", new { id }, tx, ct));
            await AuditAsync(c, tx, id, userId, workstationId, s.Id == 0 ? "CREAR_CLIENTE" : "EDITAR_CLIENTE", request.Reason, previous, persisted, ct);
            return Result<long>.Success(id);
        });
    }
    public Task<Result<long>> SaveContactAsync(CustomerContactChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Contact;
        return MutateAsync(request.CustomerId, request.ExpectedVersion, "CLIENTES.EDITAR", true, userId, ct, async (c, tx, _) =>
        {
            var previous = s.Id == 0 ? null : await c.QuerySingleOrDefaultAsync<CustomerContactData>(Cmd($"SELECT {ContactColumns} FROM cli.ClienteContactos WHERE IdCliente=@CustomerId AND IdContacto=@Id;", new { request.CustomerId, s.Id }, tx, ct));
            if (s.Id != 0 && previous is null) return Result<long>.Failure("VALIDATION", "El contacto no pertenece al cliente.");
            var p = new { request.CustomerId, s.Id, s.Name, s.Position, s.Phone, s.WhatsApp, s.Email, s.Primary, s.Active };
            var id = s.Id == 0 ? await c.ExecuteScalarAsync<long>(Cmd("INSERT INTO cli.ClienteContactos(IdCliente,Nombre,Cargo,Telefono,WhatsApp,Email,EsPrincipal,Activo) OUTPUT INSERTED.IdContacto VALUES(@CustomerId,@Name,@Position,@Phone,@WhatsApp,@Email,@Primary,@Active);", p, tx, ct)) : s.Id;
            if (s.Id != 0) await c.ExecuteAsync(Cmd("UPDATE cli.ClienteContactos SET Nombre=@Name,Cargo=@Position,Telefono=@Phone,WhatsApp=@WhatsApp,Email=@Email,EsPrincipal=@Primary,Activo=@Active WHERE IdCliente=@CustomerId AND IdContacto=@Id;", p, tx, ct));
            await TouchAsync(c, tx, request.CustomerId, ct);
            await AuditAsync(c, tx, request.CustomerId, userId, workstationId, "GUARDAR_CONTACTO_CLIENTE", request.Reason, previous, s with { Id = id }, ct);
            return Result<long>.Success(request.CustomerId);
        });
    }
    public Task<Result<long>> SaveAddressAsync(CustomerAddressChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Address;
        return MutateAsync(request.CustomerId, request.ExpectedVersion, "CLIENTES.EDITAR", true, userId, ct, async (c, tx, _) =>
        {
            var previous = s.Id == 0 ? null : await c.QuerySingleOrDefaultAsync<CustomerAddressData>(Cmd($"SELECT {AddressColumns} FROM cli.ClienteDirecciones WHERE IdCliente=@CustomerId AND IdDireccion=@Id;", new { request.CustomerId, s.Id }, tx, ct));
            if (s.Id != 0 && previous is null) return Result<long>.Failure("VALIDATION", "La dirección no pertenece al cliente.");
            var p = new { request.CustomerId, s.Id, s.Type, s.Department, s.Municipality, s.Address, s.Reference, s.Primary, s.Active };
            var id = s.Id == 0 ? await c.ExecuteScalarAsync<long>(Cmd("INSERT INTO cli.ClienteDirecciones(IdCliente,Tipo,Departamento,Municipio,Direccion,Referencia,EsPrincipal,Activo) OUTPUT INSERTED.IdDireccion VALUES(@CustomerId,@Type,@Department,@Municipality,@Address,@Reference,@Primary,@Active);", p, tx, ct)) : s.Id;
            if (s.Id != 0) await c.ExecuteAsync(Cmd("UPDATE cli.ClienteDirecciones SET Tipo=@Type,Departamento=@Department,Municipio=@Municipality,Direccion=@Address,Referencia=@Reference,EsPrincipal=@Primary,Activo=@Active WHERE IdCliente=@CustomerId AND IdDireccion=@Id;", p, tx, ct));
            await TouchAsync(c, tx, request.CustomerId, ct);
            await AuditAsync(c, tx, request.CustomerId, userId, workstationId, "GUARDAR_DIRECCION_CLIENTE", request.Reason, previous, s with { Id = id }, ct);
            return Result<long>.Success(request.CustomerId);
        });
    }
    public Task<Result<long>> SaveCreditAsync(CustomerCreditChange request, int userId, int? workstationId, CancellationToken ct = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return Invalid(error);
        var s = request.Credit;
        return MutateAsync(request.CustomerId, request.ExpectedVersion, "CLIENTES.CREDITO_CONFIGURAR", false, userId, ct, async (c, tx, _) =>
        {
            var previous = await c.QuerySingleAsync<CustomerCreditData>(Cmd($"SELECT {CreditColumns} FROM cli.Clientes WHERE IdCliente=@CustomerId;", request, tx, ct));
            await c.ExecuteAsync(Cmd("UPDATE cli.Clientes SET PermiteCredito=@Enabled,LimiteCredito=@Limit,DiasCredito=@Days,IntervaloAvisoDias=@NoticeDays,BloqueadoCredito=@Blocked,MotivoBloqueoCredito=@BlockReason WHERE IdCliente=@CustomerId;", new { request.CustomerId, s.Enabled, s.Limit, s.Days, s.NoticeDays, s.Blocked, s.BlockReason }, tx, ct));
            await AuditAsync(c, tx, request.CustomerId, userId, workstationId, "CONFIGURAR_CREDITO_CLIENTE", request.Reason, previous, s, ct);
            return Result<long>.Success(request.CustomerId);
        });
    }
    private async Task<Result<long>> MutateAsync(long id, byte[] version, string permission, bool requireActive, int userId, CancellationToken ct, Func<DbConnection, DbTransaction, CustomerData?, Task<Result<long>>> write)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await c.ExecuteAsync(Cmd(Authorization, new { userId, RequiredPermission = permission, History = false }, tx, ct));
            CustomerData? previous = null;
            if (id != 0)
            {
                previous = await c.QuerySingleOrDefaultAsync<CustomerData>(Cmd($"SELECT {Columns} FROM cli.Clientes c WITH(UPDLOCK,HOLDLOCK){Joins} WHERE c.IdCliente=@id;", new { id }, tx, ct));
                if (previous is null || !previous.RowVersion.SequenceEqual(version)) return Result<long>.Failure("CONCURRENCY_CONFLICT", "Otra estación modificó el cliente o sus datos. Recarga antes de editar.");
                if (requireActive && !previous.Active) return Result<long>.Failure("INVALID_STATE", "Reactiva el cliente antes de modificar contactos o direcciones.");
            }
            var result = await write(c, tx, previous); if (result.IsSuccess) await tx.CommitAsync(ct); return result;
        }
        catch (SqlException ex) when (ex.Number == 53301) { return Result<long>.Failure("FORBIDDEN", ex.Message); }
        catch (SqlException ex) when (ex.Number is 53501 or 53502) { return Result<long>.Failure("CODE_UNAVAILABLE", ex.Message); }
        catch (SqlException ex) when (ex.Number is 2601 or 2627) { return Result<long>.Failure("DUPLICATE_IDENTITY", "Ya existe un cliente con ese código, NIT o NRC."); }
        catch (SqlException ex) when (ex.Number == 1205) { return Result<long>.Failure("CONCURRENCY_CONFLICT", "Otra operación utilizó los datos. Recarga antes de guardar."); }
    }
    public Task<CustomerHistoryPage<CustomerSaleData>> SalesAsync(CustomerHistoryFilter f, int userId, CancellationToken ct = default) => HistoryAsync<CustomerSaleData>(f, userId,
        "ven.Ventas v", "v.IdCliente=@CustomerId", "v.IdVenta Id,v.NumeroVenta Number,v.Fecha Date,v.Estado State,v.Total,v.SaldoPendiente Pending", "v.Fecha DESC,v.IdVenta DESC", ct);
    public Task<CustomerHistoryPage<CustomerAccountData>> AccountsAsync(CustomerHistoryFilter f, int userId, CancellationToken ct = default) => HistoryAsync<CustomerAccountData>(f, userId,
        "ven.CuentasPorCobrar a JOIN ven.Ventas v ON v.IdVenta=a.IdVenta", "a.IdCliente=@CustomerId", "a.IdCuenta Id,v.NumeroVenta Sale,a.FechaEmision Issued,a.FechaVencimiento Due,a.MontoOriginal Original,a.Saldo Balance,a.Estado State", "a.FechaEmision DESC,a.IdCuenta DESC", ct);
    public Task<CustomerHistoryPage<CustomerPaymentData>> PaymentsAsync(CustomerHistoryFilter f, int userId, CancellationToken ct = default) => HistoryAsync<CustomerPaymentData>(f, userId,
        "ven.AbonosCliente p JOIN ven.CuentasPorCobrar a ON a.IdCuenta=p.IdCuenta JOIN cat.MetodosPago m ON m.IdMetodoPago=p.IdMetodoPago", "a.IdCliente=@CustomerId", "p.IdAbono Id,p.IdCuenta AccountId,p.NumeroRecibo Receipt,p.Fecha Date,p.Monto Amount,m.Nombre Method,p.Anulado Voided", "p.Fecha DESC,p.IdAbono DESC", ct);
    private async Task<CustomerHistoryPage<T>> HistoryAsync<T>(CustomerHistoryFilter f, int userId, string from, string where, string columns, string order, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await c.QueryMultipleAsync(Cmd(Authorization + $"SELECT COUNT(*) FROM {from} WHERE {where};SELECT {columns} FROM {from} WHERE {where} ORDER BY {order} OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
                new { userId, RequiredPermission = (string?)null, History = true, f.CustomerId, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }, tx, ct));
            var total = await rows.ReadSingleAsync<int>(); var items = (await rows.ReadAsync<T>()).AsList(); await tx.CommitAsync(ct); return new(items, total);
        }
        catch (SqlException ex) when (ex.Number == 53301) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }
    public async Task<CustomerBalanceData?> BalanceAsync(long id, int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await c.QuerySingleOrDefaultAsync<CustomerBalanceData>(Cmd(Authorization + """
                SELECT b.Outstanding,b.Overdue,CASE WHEN ISNULL(c.LimiteCredito,0)>b.Outstanding THEN ISNULL(c.LimiteCredito,0)-b.Outstanding ELSE 0 END Available
                FROM cli.Clientes c OUTER APPLY(SELECT ISNULL(SUM(a.Saldo),0) Outstanding,
                  ISNULL(SUM(CASE WHEN a.FechaVencimiento<CONVERT(date,SYSDATETIMEOFFSET()) THEN a.Saldo ELSE 0 END),0) Overdue
                  FROM ven.CuentasPorCobrar a WHERE a.IdCliente=c.IdCliente AND a.Saldo>0 AND a.Estado IN(N'PENDIENTE',N'PARCIAL',N'VENCIDA')) b WHERE c.IdCliente=@id;
                """, new { id, userId, RequiredPermission = (string?)null, History = true }, tx, ct));
            await tx.CommitAsync(ct); return result;
        }
        catch (SqlException ex) when (ex.Number == 53301) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }
    private static CommandDefinition Cmd(string sql, object? p, DbTransaction tx, CancellationToken ct) => new(sql, p, tx, cancellationToken: ct);
    private static Task TouchAsync(DbConnection c, DbTransaction tx, long id, CancellationToken ct) => c.ExecuteAsync(Cmd("UPDATE cli.Clientes SET NombreRazonSocial=NombreRazonSocial WHERE IdCliente=@id;", new { id }, tx, ct));
    private static Task AuditAsync(DbConnection c, DbTransaction tx, long id, int userId, int? workstationId, string action, string reason, object? before, object after, CancellationToken ct) => c.ExecuteAsync(Cmd("""
        INSERT INTO sis.Bitacora(IdUsuario,IdEquipo,Modulo,Accion,Entidad,EntidadId,Descripcion,DatosAntesJson,DatosDespuesJson)
        VALUES(@userId,@workstationId,N'Clientes',@action,N'Clientes',@id,@reason,@beforeJson,@afterJson);
        """, new { id, userId, workstationId, action, reason, beforeJson = before is null ? null : JsonSerializer.Serialize(before), afterJson = JsonSerializer.Serialize(after) }, tx, ct));
    private static Task<Result<long>> Invalid(string error) => Task.FromResult(Result<long>.Failure("VALIDATION", error));
}
