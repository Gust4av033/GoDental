using System.Data;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Repositories;

public sealed class SqlInventoryRepository(IDbConnectionFactory factory) : IInventoryRepository
{
    private const string Authorization = """
        DECLARE @Permissions TABLE(Code nvarchar(100));
        INSERT INTO @Permissions SELECT p.Codigo FROM seg.Usuarios u JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
        JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
        JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
        WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0;
        IF NOT EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'INVENTARIO.VER') THROW 53001,N'No tienes permiso para consultar inventario.',1;
        DECLARE @SeeCosts bit=CASE WHEN EXISTS(SELECT 1 FROM @Permissions WHERE Code=N'INVENTARIO.COSTOS_VER') THEN 1 ELSE 0 END;
        DECLARE @Today date=CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');
        """;

    public async Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return (await c.QueryAsync<CatalogOption>(new CommandDefinition(Authorization + "SELECT IdAlmacen Id,Nombre Name FROM inv.Almacenes WHERE Activo=1 ORDER BY Nombre;", new { userId }, cancellationToken: ct))).AsList();
    }
    public async Task<InventorySnapshot> ReadAsync(int productId, int warehouseId, int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        using var rows = await c.QueryMultipleAsync(new CommandDefinition(Authorization + """
            SELECT a.IdAlmacen WarehouseId,a.Nombre Warehouse,ISNULL(e.StockActual,0) Quantity,ISNULL(e.StockReservado,0) Reserved,
              CASE WHEN @SeeCosts=1 THEN ISNULL(e.CostoPromedio,0) END AverageCost,ISNULL(e.RowVersion,0x) RowVersion,
              CASE WHEN p.Activo=0 OR a.Activo=0 THEN 0
                WHEN p.ControlaLote=0 THEN ISNULL(e.StockActual-e.StockReservado,0)
                WHEN ISNULL(l.Vendible,0)>ISNULL(e.StockReservado,0) THEN ISNULL(l.Vendible,0)-ISNULL(e.StockReservado,0) ELSE 0 END Sellable
            FROM inv.Productos p CROSS JOIN inv.Almacenes a
            LEFT JOIN inv.ExistenciasProducto e ON e.IdProducto=p.IdProducto AND e.IdAlmacen=a.IdAlmacen
            OUTER APPLY(SELECT SUM(el.StockActual) Vendible FROM inv.ExistenciasLote el JOIN inv.LotesProducto lp ON lp.IdLote=el.IdLote
              WHERE el.IdAlmacen=a.IdAlmacen AND lp.IdProducto=p.IdProducto AND lp.Activo=1
              AND (lp.FechaVencimiento>=@Today OR (lp.FechaVencimiento IS NULL AND p.ControlaVencimiento=0))) l
            WHERE p.IdProducto=@productId ORDER BY a.Nombre;
            SELECT lp.IdLote Id,lp.NumeroLote Number,lp.FechaFabricacion Manufactured,lp.FechaVencimiento Expires,
              ISNULL(el.StockActual,0) Quantity,CASE WHEN @SeeCosts=1 THEN lp.CostoUnitarioIngreso END EntryCost,lp.Activo Active,
              CASE WHEN lp.Activo=0 THEN N'Bloqueado' WHEN lp.FechaVencimiento<@Today THEN N'Vencido'
                WHEN p.ControlaVencimiento=1 AND lp.FechaVencimiento IS NULL THEN N'Sin fecha'
                WHEN ISNULL(el.StockActual,0)=0 THEN N'Agotado'
                WHEN lp.FechaVencimiento<=DATEADD(day,ISNULL(p.DiasAlertaVencimiento,90),@Today) THEN N'Próximo a vencer' ELSE N'Vigente' END Status
            FROM inv.LotesProducto lp JOIN inv.Productos p ON p.IdProducto=lp.IdProducto
            LEFT JOIN inv.ExistenciasLote el ON el.IdLote=lp.IdLote AND el.IdAlmacen=@warehouseId
            WHERE lp.IdProducto=@productId ORDER BY CASE WHEN lp.FechaVencimiento IS NULL THEN 1 ELSE 0 END,lp.FechaVencimiento,lp.IdLote;
            """, new { productId, warehouseId, userId }, cancellationToken: ct));
        return new((await rows.ReadAsync<StockData>()).AsList(), (await rows.ReadAsync<LotData>()).AsList());
    }
    public async Task<MovementPage> MovementsAsync(MovementFilter filter, int userId, CancellationToken ct = default)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        const string where = """
            WHERE m.IdProducto=@ProductId AND (@WarehouseId IS NULL OR m.IdAlmacen=@WarehouseId)
            AND (@LotId IS NULL OR m.IdLote=@LotId) AND (@Type IS NULL OR m.TipoMovimiento=@Type)
            AND (@From IS NULL OR m.Fecha>=@From) AND (@End IS NULL OR m.Fecha<@End)
            """;
        var start = filter.From is { } from ? new DateTimeOffset?(new(DateTime.SpecifyKind(from.Date, DateTimeKind.Unspecified), TimeSpan.FromHours(-6))) : null;
        var end = filter.Until is { } until ? new DateTimeOffset?(new(DateTime.SpecifyKind(until.Date.AddDays(1), DateTimeKind.Unspecified), TimeSpan.FromHours(-6))) : null;
        using var rows = await c.QueryMultipleAsync(new CommandDefinition(Authorization + $"""
            SELECT COUNT(*) FROM inv.MovimientosInventario m {where};
            SELECT m.IdMovimiento Id,SWITCHOFFSET(m.Fecha,'-06:00') Date,a.Nombre Warehouse,l.NumeroLote Lot,m.TipoMovimiento Type,
              m.Cantidad Quantity,m.ExistenciaAnterior [Before],m.ExistenciaPosterior [After],
              CASE WHEN @SeeCosts=1 THEN m.CostoUnitario END Cost,u.NombreCompleto [User],
              CASE WHEN m.ReferenciaId IS NOT NULL THEN CONCAT(m.ReferenciaTipo,N' #',m.ReferenciaId) END Reference,m.Observacion Reason
            FROM inv.MovimientosInventario m JOIN inv.Almacenes a ON a.IdAlmacen=m.IdAlmacen
            JOIN seg.Usuarios u ON u.IdUsuario=m.IdUsuario LEFT JOIN inv.LotesProducto l ON l.IdLote=m.IdLote
            {where} ORDER BY m.IdMovimiento DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, filter.ProductId, filter.WarehouseId, filter.LotId, filter.Type, From = start, End = end,
                Offset = checked((filter.Page - 1) * filter.PageSize), filter.PageSize }, cancellationToken: ct));
        var total = await rows.ReadSingleAsync<int>();
        return new((await rows.ReadAsync<MovementData>()).AsList(), total);
    }
    public Task<Result<long>> AdjustAsync(InventoryAdjustment r, int userId, int? workstationId, CancellationToken ct = default) => ExecuteAsync(
        "inv.sp_RegistrarAjuste", new { r.ProductId, r.WarehouseId, r.Quantity, r.UnitCost, r.Type, r.Reason, r.ExpectedVersion,
            r.LotId, r.NewLotNumber, r.Manufactured, r.Expires, r.DestinationWarehouseId, userId, workstationId }, ct);
    public Task<Result<long>> SetLotStateAsync(LotStateChange r, int userId, int? workstationId, CancellationToken ct = default) => ExecuteAsync(
        "inv.sp_CambiarEstadoLote", new { r.LotId, r.ExpectedActive, r.Active, r.Reason, userId, workstationId }, ct);
    private async Task<Result<long>> ExecuteAsync(string procedure, object parameters, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        try { return Result<long>.Success(await c.ExecuteScalarAsync<long>(new CommandDefinition(procedure, parameters, commandType: CommandType.StoredProcedure, cancellationToken: ct))); }
        catch (SqlException ex) when (ex.Number is >= 53001 and <= 53004)
        { return Result<long>.Failure(ex.Number switch { 53001 => "FORBIDDEN", 53003 => "CONCURRENCY_CONFLICT", 53004 => "INSUFFICIENT_STOCK", _ => "VALIDATION" }, ex.Message); }
        catch (SqlException ex) when (ex.Number == 1205)
        { return Result<long>.Failure("CONCURRENCY_CONFLICT", "Otra operación utilizó estos saldos. Actualiza y revisa antes de repetir."); }
    }
}
