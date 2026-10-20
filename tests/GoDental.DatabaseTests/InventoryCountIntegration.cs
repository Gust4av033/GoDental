using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class InventoryCountIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root)
    {
        var checks = 0;
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            checks++; Console.WriteLine("OK " + label);
        }
        await using var connection = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("conteos_test", "Administrador conteos",
            Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "COUNTS-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity);
        var products = new ProductService(new SqlProductRepository(factory), session);
        var inventory = new InventoryService(new SqlInventoryRepository(factory), session);
        var repository = new SqlInventoryCountRepository(factory);
        var service = new InventoryCountService(repository, session);
        var catalogs = (await products.GetCatalogsAsync()).Value!;
        var warehouse = (await inventory.WarehousesAsync()).First().Id;
        async Task<int> Product(string sku, bool lots = false)
        {
            var result = await products.SaveAsync(new() { Sku = sku, Name = sku, CategoryId = catalogs.Categories.First().Id,
                UnitId = (short)catalogs.Units.First().Id, TracksLot = lots, TracksExpiry = lots });
            Check(result.IsSuccess, "Producto de prueba " + sku); return result.Value;
        }
        async Task<Result<long>> Adjust(int product, decimal quantity, decimal? cost = null, long? lot = null, string? newLot = null)
        {
            var version = (await inventory.ReadAsync(product, warehouse)).Stocks.Single(s => s.WarehouseId == warehouse).RowVersion;
            return await inventory.AdjustAsync(new(product, warehouse, quantity, cost, "AJUSTE", "Preparación de prueba de conteo", version,
                lot, newLot, Expires: newLot is null ? null : DateTime.Today.AddYears(2)));
        }
        async Task<InventoryCountSnapshot> Read(long count) => (await service.ReadAsync(count))!;
        async Task<long> Open(params int[] ids)
        {
            var result = await service.StartAsync(new(warehouse, ids, "Conteo físico aislado"));
            Check(result.IsSuccess, "Abre conteo transaccional"); return result.Value;
        }
        async Task<Result<long>> Capture(long count, Func<InventoryCountLineData, decimal> quantity)
        {
            var snapshot = await Read(count);
            return await service.CaptureAsync(new(count, snapshot.Header.RowVersion, snapshot.Lines.Select(l => new InventoryCountQuantity(l.Id, quantity(l))).ToArray()));
        }
        async Task<Result<long>> Close(long count) => await service.CloseAsync(new(count, (await Read(count)).Header.RowVersion, "Diferencias verificadas físicamente"));
        async Task<Result<long>> Cancel(long count) => await service.CancelAsync(new(count, (await Read(count)).Header.RowVersion, "Revisión y nuevo conteo"));
        async Task<decimal> Stock(int product) => await connection.ExecuteScalarAsync<decimal>(
            "SELECT StockActual FROM inv.ExistenciasProducto WHERE IdAlmacen=@warehouse AND IdProducto=@product", new { warehouse, product });
        async Task<int> CountMovements(long count) => await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inv.MovimientosInventario WHERE ReferenciaTipo=N'CONTEO' AND ReferenciaId=@count", new { count });
        var simple = await Product("CNT-SIMPLE"); var second = await Product("CNT-SEGUNDO"); var tracked = await Product("CNT-LOTES", true);
        var missingStock = await Product("CNT-SIN-SALDO");
        Check((await Adjust(simple, 10, 2)).IsSuccess && (await Adjust(second, 5, 3)).IsSuccess, "Saldos simples establecidos");
        Check((await Adjust(tracked, 6, 4, newLot: "CNT-A")).IsSuccess && (await Adjust(tracked, 4, 6, newLot: "CNT-B")).IsSuccess,
            "Establece dos lotes con costos históricos distintos");
        var lots = (await inventory.ReadAsync(tracked, warehouse)).Lots;
        var lotA = lots.Single(l => l.Number == "CNT-A").Id; var lotB = lots.Single(l => l.Number == "CNT-B").Id;
        Check((await Adjust(tracked, 1, 4.8m, newLot: "CNT-CERO")).IsSuccess, "Crea tercer lote");
        var zeroLot = (await inventory.ReadAsync(tracked, warehouse)).Lots.Single(l => l.Number == "CNT-CERO").Id;
        Check((await Adjust(tracked, -1, lot: zeroLot)).IsSuccess, "Conserva saldo de lote cero");
        Check((await service.StartAsync(new(warehouse, [missingStock], "Sin inventario inicial"))).ErrorCode == "VALIDATION", "No usa conteos para crear inventario inicial");
        await connection.ExecuteAsync("UPDATE inv.ExistenciasProducto SET StockReservado=10 WHERE IdProducto=@tracked AND IdAlmacen=@warehouse", new { tracked, warehouse });
        var first = await Open(simple, tracked);
        var initial = await Read(first);
        Check(initial.Lines.Count == 4 && initial.Lines.All(l => !l.Counted && l.Difference is null && !l.RequiresRecount), "Fotografía incluye todos los lotes y diferencia pendiente");
        Check(initial.Header.TotalLines == 4 && initial.Header.CountedLines == 0 && initial.Header.RowVersion.Length == 8, "Cabecera informa progreso y versión");
        Check(initial.Lines.Single(l => l.LotId == zeroLot).SystemQuantity == 0, "Incluye lotes de saldo cero");
        Check((await service.StartAsync(new(warehouse, [simple], "Conteo duplicado"))).ErrorCode == "INVALID_STATE", "Impide producto solapado en dos conteos abiertos");
        Check((await Close(first)).ErrorCode == "VALIDATION" && await CountMovements(first) == 0, "No cierra líneas pendientes ni genera bajas falsas");
        var simpleLine = initial.Lines.Single(l => l.ProductId == simple);
        var simultaneous = new CaptureInventoryCount(first, initial.Header.RowVersion, [new(simpleLine.Id, 0)]);
        var captures = await Task.WhenAll(service.CaptureAsync(simultaneous), service.CaptureAsync(simultaneous));
        Check(captures.Count(r => r.IsSuccess) == 1 && captures.Count(r => r.ErrorCode == "CONCURRENCY_CONFLICT") == 1,
            "Dos estaciones capturando: una gana, otra recibe conflicto");
        var partial = await Read(first);
        Check(partial.Header.CountedLines == 1 && partial.Lines.Single(l => l.Id == simpleLine.Id).Difference == -10, "Cero físico explícito no equivale a pendiente");
        Check(await Stock(simple) == 10, "Guardar cantidades no modifica stock");
        Check((await service.CaptureAsync(new(first, partial.Header.RowVersion, [new(long.MaxValue, 2)]))).ErrorCode == "VALIDATION", "No captura líneas ajenas al conteo");
        Check((await Capture(first, l => l.ProductId == simple ? 8 : l.LotId == lotA ? 7 : l.LotId == lotB ? 3 : 0)).IsSuccess,
            "Captura diferencias positivas, negativas y sin diferencia");
        var beforeClose = await Read(first);
        var finish = new FinalizeInventoryCount(first, beforeClose.Header.RowVersion, "Conteo aprobado");
        var closes = await Task.WhenAll(service.CloseAsync(finish), service.CloseAsync(finish));
        Check(closes.Count(r => r.IsSuccess) == 1 && closes.Count(r => r.ErrorCode is "INVALID_STATE" or "CONCURRENCY_CONFLICT") == 1, "Cierre concurrente se aplica exactamente una vez");
        Check(await Stock(simple) == 8 && await Stock(tracked) == 10, "Cierra físico y compensa lotes sin invadir reservas");
        Check(await CountMovements(first) == 3, "Kardex sólo contiene las tres diferencias no nulas");
        Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE ReferenciaTipo=N'CONTEO' AND ReferenciaId=@first AND ExistenciaPosterior<>ExistenciaAnterior+Cantidad", new { first }) == 0,
            "Todos los movimientos de conteo cuadran");
        var closed = await Read(first);
        Check(closed.Header.State == "CERRADO" && closed.Header.Closed is not null && closed.Header.ClosedBy == identity.DisplayName && closed.Lines.Count(l => l.AdjustmentGenerated) == 3,
            "Cierre conserva autor, fecha y marcas de ajuste");
        Check((await service.CaptureAsync(simultaneous)).ErrorCode == "INVALID_STATE" && (await Cancel(first)).ErrorCode == "INVALID_STATE", "Conteo cerrado inmutable");
        var stock = (await inventory.ReadAsync(tracked, warehouse)).Stocks.Single(s => s.WarehouseId == warehouse);
        lots = (await inventory.ReadAsync(tracked, warehouse)).Lots;
        Check(stock.AverageCost == 4.8m && lots.Single(l => l.Id == lotA).EntryCost == 4 && lots.Single(l => l.Id == lotB).EntryCost == 6,
            "Conserva promedio y costos históricos por lote");
        Check(lots.Single(l => l.Id == lotA).Quantity == 7 && lots.Single(l => l.Id == lotB).Quantity == 3, "Lotes y agregado conservan consistencia");
        var cancellation = await Open(simple);
        Check((await Cancel(cancellation)).IsSuccess && (await Read(cancellation)).Header.State == "CANCELADO" && await CountMovements(cancellation) == 0, "Cancelación conserva historial sin afectar stock");
        Check(await Stock(simple) == 8, "Cancelar no cambia cantidades");
        var rollback = await Open(simple, second);
        Check((await Capture(rollback, l => l.SystemQuantity - 1)).IsSuccess, "Captura conteo para probar rollback");
        var rollbackSnapshot = await Read(rollback);
        var failed = false;
        try { await repository.FinalizeAsync(new(rollback, rollbackSnapshot.Header.RowVersion, "Fallo de auditoría controlado"), false, identity.Id, int.MaxValue); }
        catch (SqlException ex) when (ex.Number == 547) { failed = true; }
        var afterFailure = await Read(rollback);
        Check(failed && await Stock(simple) == 8 && await Stock(second) == 5 && await CountMovements(rollback) == 0, "Fallo de auditoría revierte todos los productos y Kardex");
        Check(afterFailure.Header.State == "ABIERTO" && afterFailure.Header.RowVersion.SequenceEqual(rollbackSnapshot.Header.RowVersion)
            && afterFailure.Lines.All(l => !l.AdjustmentGenerated), "Rollback conserva cabecera, versión y marcas originales");
        Check((await Close(rollback)).IsSuccess && await Stock(simple) == 7 && await Stock(second) == 4, "Se puede cerrar tras resolver el fallo sin duplicar");
        var stale = await Open(simple);
        Check((await Capture(stale, l => 6)).IsSuccess, "Captura conteo previo a movimiento");
        Check((await Adjust(simple, 1, 2)).IsSuccess && (await Adjust(simple, -1)).IsSuccess, "Simula saldo que cambia y vuelve al mismo valor");
        Check((await Read(stale)).Lines.All(l => l.RequiresRecount), "Detecta fotografía obsoleta aunque el saldo sea igual");
        Check((await Close(stale)).ErrorCode == "CONCURRENCY_CONFLICT" && await CountMovements(stale) == 0, "No aplica conteo obsoleto sobre movimientos posteriores");
        Check((await Cancel(stale)).IsSuccess, "Permite cancelar fotografía obsoleta");
        await connection.ExecuteAsync("UPDATE inv.ExistenciasProducto SET StockReservado=6 WHERE IdProducto=@simple AND IdAlmacen=@warehouse", new { simple, warehouse });
        var reserved = await Open(simple);
        Check((await Capture(reserved, l => 5.9999m)).IsSuccess && (await Close(reserved)).ErrorCode == "INSUFFICIENT_STOCK" && await Stock(simple) == 7, "Protege stock reservado incluso por una diferencia de 0.0001");
        Check((await Cancel(reserved)).IsSuccess, "Cancela conteo con reservas pendientes");
        await connection.ExecuteAsync("UPDATE inv.ExistenciasProducto SET StockReservado=0 WHERE IdProducto IN(@simple,@tracked) AND IdAlmacen=@warehouse", new { simple, tracked, warehouse });
        var changingLot = await Open(tracked);
        Check((await Capture(changingLot, l => l.SystemQuantity)).IsSuccess, "Captura lotes sin diferencia");
        await connection.ExecuteAsync("UPDATE inv.ExistenciasLote SET StockActual=StockActual WHERE IdAlmacen=@warehouse AND IdLote=@lotA", new { warehouse, lotA });
        Check((await Read(changingLot)).Lines.Single(l => l.LotId == lotA).RequiresRecount && (await Close(changingLot)).ErrorCode == "CONCURRENCY_CONFLICT", "Controla versión del lote además del saldo agregado");
        Check((await Cancel(changingLot)).IsSuccess, "Cancela conteo de lote obsoleto");
        await connection.ExecuteAsync("UPDATE inv.LotesProducto SET Activo=0,FechaVencimiento=DATEADD(day,-1,CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time')) WHERE IdLote=@lotA", new { lotA });
        var blocked = await Open(tracked);
        Check((await Capture(blocked, l => l.SystemQuantity + (l.LotId == lotA ? 1 : 0))).IsSuccess && (await Close(blocked)).ErrorCode == "VALIDATION", "Cuenta lote bloqueado/vencido pero rechaza entrada positiva");
        Check((await Capture(blocked, l => l.SystemQuantity - (l.LotId == lotA ? 1 : 0))).IsSuccess && (await Close(blocked)).IsSuccess, "Admite corrección física negativa sin reactivar lote vencido");
        Check(!(await inventory.ReadAsync(tracked, warehouse)).Lots.Single(l => l.Id == lotA).Active, "Cierre no reactiva lotes");
        var unchanged = await Open(simple);
        Check((await Capture(unchanged, l => l.SystemQuantity)).IsSuccess && (await Close(unchanged)).IsSuccess && await CountMovements(unchanged) == 0, "Conteo sin diferencias cierra sin movimiento cero");
        var permission = await Open(simple);
        await Capture(permission, l => l.SystemQuantity);
        var permissionVersion = (await Read(permission)).Header.RowVersion;
        await connection.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.AJUSTAR')");
        Check((await Close(permission)).ErrorCode == "FORBIDDEN", "SQL revalida permiso de cierre revocado aunque C# lo tenga cacheado");
        await connection.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.CONTEO')");
        Check((await service.CancelAsync(new(permission, permissionVersion, "Permiso revocado"))).ErrorCode == "FORBIDDEN"
            && (await service.StartAsync(new(warehouse, [second], "Sin permiso"))).ErrorCode == "FORBIDDEN", "SQL revalida permiso de conteo para cancelar y abrir");
        var denied = false;
        try { await Read(permission); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "Lectura también revalida permisos en SQL");
        await connection.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdRol=(SELECT IdRol FROM seg.Roles WHERE Nombre=N'Administrador') AND IdPermiso IN(SELECT IdPermiso FROM seg.Permisos WHERE Codigo IN(N'INVENTARIO.CONTEO',N'INVENTARIO.AJUSTAR'))");
        Check((await Cancel(permission)).IsSuccess, "Restaura permisos de prueba y cancela conteo");
        var page = await service.SearchAsync(new(warehouse, "CERRADO", PageSize: 2));
        Check(page.Total >= 4 && page.Items.Count == 2 && page.Items.All(i => i.State == "CERRADO"), "Consulta paginada y filtrada de conteos");
        Check(await service.ReadAsync(long.MaxValue) is null, "Conteo inexistente devuelve ausencia explícita");
        Check(await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sis.Bitacora WHERE Entidad=N'ConteosInventario' AND EntidadId=@first AND Accion=N'CERRAR_CONTEO'", new { first }) == 1,
            "Un solo evento de cierre auditado pese a reintento concurrente");
        var malformed = await Open(second);
        var malformedVersion = (await Read(malformed)).Header.RowVersion;
        await DevelopmentDatabase.InstallAsync(connection.Database, Path.Combine(root, "database"));
        var migratedAgain = await Read(malformed);
        Check(migratedAgain.Header.RowVersion.SequenceEqual(malformedVersion) && migratedAgain.Lines.All(l => !l.Counted)
            && await CountMovements(first) == 3, "Reaplicar migraciones conserva fotografías, pendientes e historial");
        var invalidJson = JsonSerializer.Serialize(new[] { new { LineId = (await Read(malformed)).Lines.Single().Id, Quantity = .00001m } });
        Check((await RawCapture(invalidJson)) == 53102 && (await RawCapture("[]")) == 53102 && (await RawCapture("no es json")) == 53102,
            "SQL rechaza precisión inválida y JSON inválido aun evitando los validadores C#");
        async Task<int> RawCapture(string json)
        {
            try
            {
                await connection.ExecuteAsync("inv.sp_CapturarConteo", new { CountId = malformed, ExpectedVersion = malformedVersion, LinesJson = json,
                    UserId = identity.Id, WorkstationId = identity.WorkstationId }, commandType: CommandType.StoredProcedure);
                return 0;
            }
            catch (SqlException ex) { return ex.Number; }
        }
        Check((await Cancel(malformed)).IsSuccess, "No corrompe conteo tras solicitudes malformadas");
        Console.WriteLine($"{checks} comprobaciones de conteos correctas.");
    }
}
