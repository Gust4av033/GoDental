using System.Data;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Repositories;

public sealed class SqlInventoryCountRepository(IDbConnectionFactory factory) : IInventoryCountRepository
{
    private const string Authorization = """
        IF (SELECT COUNT(DISTINCT p.Codigo) FROM seg.Usuarios u
            JOIN seg.UsuarioRoles ur ON ur.IdUsuario=u.IdUsuario
            JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1
            JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
            JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1
            WHERE u.IdUsuario=@userId AND u.Activo=1 AND u.DebeCambiarClave=0
            AND p.Codigo IN(N'INVENTARIO.VER',N'INVENTARIO.CONTEO'))<>2
            THROW 53101,N'No tienes permiso para consultar conteos.',1;
        """;
    private const string Header = """
        SELECT h.IdConteo Id,h.IdAlmacen WarehouseId,a.Nombre Warehouse,h.Estado State,
            SWITCHOFFSET(h.FechaInicio,'-06:00') Opened,SWITCHOFFSET(h.FechaCierre,'-06:00') Closed,
            u.NombreCompleto CreatedBy,uc.NombreCompleto ClosedBy,h.Observaciones Notes,h.RowVersion,
            (SELECT COUNT(*) FROM inv.ConteoInventarioDetalle d WHERE d.IdConteo=h.IdConteo) TotalLines,
            (SELECT COUNT(*) FROM inv.ConteoInventarioDetalle d WHERE d.IdConteo=h.IdConteo AND d.Contado=1) CountedLines
        FROM inv.ConteosInventario h JOIN inv.Almacenes a ON a.IdAlmacen=h.IdAlmacen
        JOIN seg.Usuarios u ON u.IdUsuario=h.CreadoPor LEFT JOIN seg.Usuarios uc ON uc.IdUsuario=h.CerradoPor
        """;

    public async Task<InventoryCountPage> SearchAsync(InventoryCountFilter filter, int userId, CancellationToken ct = default)
    {
        const string where = " WHERE (@WarehouseId IS NULL OR h.IdAlmacen=@WarehouseId) AND (@State IS NULL OR h.Estado=@State) ";
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await connection.QueryMultipleAsync(new CommandDefinition(Authorization +
                "SELECT COUNT(*) FROM inv.ConteosInventario h" + where + ";" + Header + where +
                "ORDER BY h.IdConteo DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
                new { userId, filter.WarehouseId, filter.State, Offset = checked((filter.Page - 1) * filter.PageSize), filter.PageSize }, tx, cancellationToken: ct));
            var total = await rows.ReadSingleAsync<int>();
            var items = (await rows.ReadAsync<InventoryCountData>()).AsList();
            await tx.CommitAsync(ct);
            return new(items, total);
        }
        catch (SqlException ex) when (ex.Number == 53101) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }

    public async Task<InventoryCountSnapshot?> ReadAsync(long countId, int userId, CancellationToken ct = default)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var rows = await connection.QueryMultipleAsync(new CommandDefinition(Authorization + Header + " WHERE h.IdConteo=@countId;" + """
                SELECT d.IdDetalle Id,d.IdProducto ProductId,p.SKU Sku,p.Nombre Product,d.IdLote LotId,l.NumeroLote Lot,
                    d.CantidadSistema SystemQuantity,d.CantidadContada CountedQuantity,d.Contado Counted,d.AjusteGenerado AdjustmentGenerated,
                    CONVERT(bit,CASE WHEN h.Estado=N'ABIERTO' AND (d.VersionExistencia IS NULL OR e.RowVersion IS NULL
                        OR d.VersionExistencia<>e.RowVersion OR (d.IdLote IS NOT NULL AND (el.RowVersion IS NULL OR d.VersionLote IS NULL OR d.VersionLote<>el.RowVersion)))
                        THEN 1 ELSE 0 END) RequiresRecount
                FROM inv.ConteoInventarioDetalle d JOIN inv.ConteosInventario h ON h.IdConteo=d.IdConteo
                JOIN inv.Productos p ON p.IdProducto=d.IdProducto LEFT JOIN inv.LotesProducto l ON l.IdLote=d.IdLote
                LEFT JOIN inv.ExistenciasProducto e ON e.IdAlmacen=h.IdAlmacen AND e.IdProducto=d.IdProducto
                LEFT JOIN inv.ExistenciasLote el ON el.IdAlmacen=h.IdAlmacen AND el.IdLote=d.IdLote
                WHERE d.IdConteo=@countId ORDER BY d.IdProducto,d.IdLote;
                """, new { countId, userId }, tx, cancellationToken: ct));
            var header = await rows.ReadSingleOrDefaultAsync<InventoryCountData>();
            var lines = (await rows.ReadAsync<InventoryCountLineData>()).AsList();
            await tx.CommitAsync(ct);
            return header is null ? null : new(header, lines);
        }
        catch (SqlException ex) when (ex.Number == 53101) { throw new UnauthorizedAccessException(ex.Message, ex); }
    }

    public Task<Result<long>> StartAsync(StartInventoryCount request, int userId, int? workstationId, CancellationToken ct = default) =>
        ExecuteAsync("inv.sp_AbrirConteo", new { request.WarehouseId, ProductsJson = JsonSerializer.Serialize(request.ProductIds), request.Reason, userId, workstationId }, ct);

    public Task<Result<long>> CaptureAsync(CaptureInventoryCount request, int userId, int? workstationId, CancellationToken ct = default) =>
        ExecuteAsync("inv.sp_CapturarConteo", new { request.CountId, request.ExpectedVersion, LinesJson = JsonSerializer.Serialize(request.Lines), userId, workstationId }, ct);

    public Task<Result<long>> FinalizeAsync(FinalizeInventoryCount request, bool cancel, int userId, int? workstationId, CancellationToken ct = default) =>
        ExecuteAsync("inv.sp_FinalizarConteo", new { request.CountId, request.ExpectedVersion, request.Reason, Cancel = cancel, userId, workstationId }, ct);

    private async Task<Result<long>> ExecuteAsync(string procedure, object parameters, CancellationToken ct)
    {
        await using var connection = await factory.OpenConnectionAsync(ct);
        try
        {
            return Result<long>.Success(await connection.ExecuteScalarAsync<long>(new CommandDefinition(procedure, parameters,
                commandType: CommandType.StoredProcedure, cancellationToken: ct)));
        }
        catch (SqlException ex) when (ex.Number is >= 53101 and <= 53105)
        {
            return Result<long>.Failure(ex.Number switch
            {
                53101 => "FORBIDDEN", 53103 => "CONCURRENCY_CONFLICT", 53104 => "INSUFFICIENT_STOCK", 53105 => "INVALID_STATE", _ => "VALIDATION"
            }, ex.Message);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            return Result<long>.Failure("CONCURRENCY_CONFLICT", "Otra operación utilizó estos datos. Recarga y revisa el conteo antes de repetir.");
        }
    }
}
