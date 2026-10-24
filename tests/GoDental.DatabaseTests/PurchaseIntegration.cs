using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class PurchaseIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory, string root, string database)
    {
        var checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; Console.WriteLine("OK " + message); }
        long Saved(Result<MvpReceipt> result) => result.IsSuccess ? result.Value!.Id : throw new InvalidOperationException($"{result.ErrorCode}: {result.Message}");
        await using var c = await factory.OpenConnectionAsync();
        var gateway = new SqlIdentityGateway(factory, new Pbkdf2PasswordHasher());
        var identity = (await gateway.CreateAdministratorAsync("purchase_test", "Administrador de prueba aislada", Convert.ToHexString(RandomNumberGenerator.GetBytes(24)), "PURCHASE-TEST")).Value!;
        var session = new UserSession(); session.Begin(identity); var repo = new SqlMvpRepository(factory); var service = new PurchaseService(repo, session);
        var suppliers = new SupplierService(new SqlSupplierRepository(factory), session);
        var supplier = (await suppliers.SaveAsync(new(new() { Name = "Proveedor de pruebas" }, "Alta aislada"))).Value;
        var products = new ProductService(new SqlProductRepository(factory), session); var productCatalogs = (await products.GetCatalogsAsync()).Value!;
        async Task<int> Product(string sku, bool lot = false)
        {
            var result = await products.SaveAsync(new() { Sku = sku, Name = "Producto " + sku, CategoryId = productCatalogs.Categories[0].Id, UnitId = (short)productCatalogs.Units.First(u => u.Name.Contains("Unidad")).Id, Price = 100, TracksLot = lot, TracksExpiry = lot });
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message); return checked((int)result.Value);
        }
        var p = await Product("CMP-001"); var lotProduct = await Product("CMP-LOTE", true); var freshProduct = await Product("CMP-NUEVO");
        var catalogs = await service.CatalogsAsync(); var usd = catalogs.Currencies.Single(x => x.Code == "USD").Id; var warehouse = catalogs.Warehouses.First().Id;
        SavePurchaseOrder Create(int product, decimal quantity, decimal cost, short? currency = null, decimal rate = 1) => new(Guid.NewGuid(), 0, [], supplier, currency ?? usd, rate, DateTime.Today, [new(product, quantity, cost)], "Orden aislada");
        async Task<PurchaseOrderSnapshot> Read(long id) => (await service.ReadAsync(id))!;
        async Task<PurchaseOrderSnapshot> Confirm(SavePurchaseOrder request)
        {
            var id = Saved(await service.SaveAsync(request)); var order = await Read(id);
            Saved(await service.TransitionAsync(new(Guid.NewGuid(), id, order.Order.RowVersion, "CONFIRMADA", "Confirmación aislada"))); return await Read(id);
        }
        async Task<PurchaseReceiptSnapshot> Prepare(PurchaseOrderSnapshot order, IReadOnlyList<ReceivePurchaseLine> lines, bool complete = true, decimal rate = 1)
        {
            var id = Saved(await service.PrepareAsync(new(Guid.NewGuid(), order.Order.Id, order.Order.RowVersion, warehouse, rate, complete, lines, "Recepción aislada"))); return (await service.ReceiptAsync(id))!;
        }
        async Task<PurchaseReceiptAction> ApplyRequest(PurchaseReceiptSnapshot receipt, bool apply = true) => new(Guid.NewGuid(), receipt.Receipt.Id, receipt.Receipt.RowVersion, (await Read(receipt.Receipt.OrderId)).Order.RowVersion, apply, "Aplicación aislada");
        async Task<string> InventoryState() => JsonSerializer.Serialize(await c.QuerySingleAsync("SELECT (SELECT SUM(StockActual) FROM inv.ExistenciasProducto) Stock,(SELECT SUM(StockActual*CostoPromedio) FROM inv.ExistenciasProducto) Cost,(SELECT COUNT(*) FROM inv.LotesProducto) Lots,(SELECT COUNT(*) FROM inv.MovimientosInventario) Movements,(SELECT SUM(CantidadRecibida) FROM cmp.OrdenCompraDetalle) Received,(SELECT COUNT(*) FROM sis.OperacionesMvp) Ledger;"));
        var create = Create(p, 10, 20);
        var pair = await Task.WhenAll(service.SaveAsync(create), service.SaveAsync(create));
        Check(pair.All(x => x.IsSuccess) && pair.Select(x => x.Value!.Id).Distinct().Count() == 1, "Reenvío simultáneo crea una sola OC y un solo correlativo");
        var orderId = pair[0].Value!.Id; var draft = await Read(orderId);
        Check(draft.Order.State == "BORRADOR" && draft.Order.Number.StartsWith("OC-") && draft.Lines.Single().Pending == 10, "OC inicia en borrador con correlativo y pendiente servidor");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario;") == 0, "Crear una OC no modifica inventario");
        var edited = create with { OperationId = Guid.NewGuid(), Id = orderId, ExpectedVersion = draft.Order.RowVersion, Lines = [new(p, 10, 21, 10)] };
        var edits = await Task.WhenAll(service.SaveAsync(edited), service.SaveAsync(edited with { OperationId = Guid.NewGuid() }));
        Check(edits.Count(x => x.IsSuccess) == 1 && edits.Count(x => x.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos ediciones de borrador no se sobrescriben");
        draft = await Read(orderId);
        Check(draft.Order.Subtotal == 210 && draft.Order.Total == 200 && draft.Lines.Single().Discount == 10, "Servidor recalcula totales y descuento en moneda origen");
        Check((await service.PrepareAsync(new(Guid.NewGuid(), orderId, draft.Order.RowVersion, warehouse, 1, true, [new(draft.Lines[0].Id, 1)], "No confirmada"))).ErrorCode == "INVALID_STATE", "No recibe una OC en borrador");
        Saved(await service.TransitionAsync(new(Guid.NewGuid(), orderId, draft.Order.RowVersion, "CONFIRMADA", "Compra confirmada"))); draft = await Read(orderId);
        Check((await service.SaveAsync(edited with { OperationId = Guid.NewGuid(), ExpectedVersion = draft.Order.RowVersion })).ErrorCode == "INVALID_STATE", "Una OC confirmada no reescribe costos ni líneas");
        var setup = new InventorySetupService(repo, session); Saved(await setup.InitializeAsync(new(Guid.NewGuid(), p, warehouse, 10, 10, "Stock previo")));
        var receipt = await Prepare(draft, [new(draft.Lines[0].Id, 4, 2)]);
        Check(receipt.Receipt.State == "BORRADOR" && receipt.Lines.Single().PurchaseCostUsd == 20 && receipt.Lines.Single().LandedUnitCost == 22, "Recepción calcula descuento unitario y costo puesto bodega");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p;", new { p }) == 10, "Borrador de recepción no aumenta stock");
        var apply = await ApplyRequest(receipt); var applied = await Task.WhenAll(service.ApplyAsync(apply), service.ApplyAsync(apply));
        Check(applied.All(x => x.IsSuccess) && applied.Select(x => x.Value!.Id).Distinct().Count() == 1, "Dos reenvíos de aplicación no duplican entrada");
        var partial = await Read(orderId);
        Check(partial.Order.State == "RECIBIDA_PARCIAL" && partial.Lines.Single().Received == 4 && partial.Lines.Single().Pending == 6, "Aplicación parcial actualiza estado y pendiente de OC");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p;", new { p }) == 14 && await c.ExecuteScalarAsync<decimal>("SELECT CostoPromedio FROM inv.ExistenciasProducto WHERE IdProducto=@p;", new { p }) == 13.428571m, "Promedio móvil combina stock anterior y costo real a seis decimales");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE TipoMovimiento=N'COMPRA' AND ReferenciaId=@id;", new { id = receipt.Receipt.Id }) == 1, "Kardex conserva una sola entrada con origen recepción");
        Check((await service.TransitionAsync(new(Guid.NewGuid(), orderId, partial.Order.RowVersion, "CANCELADA", "No reversible"))).ErrorCode == "INVALID_STATE", "No cancela OC con stock recibido");
        Check((await service.ApplyAsync(apply with { OperationId = Guid.NewGuid(), ExpectedOrderVersion = partial.Order.RowVersion, ExpectedVersion = (await service.ReceiptAsync(receipt.Receipt.Id))!.Receipt.RowVersion })).ErrorCode == "INVALID_STATE", "Otro GUID no vuelve a aplicar una recepción");
        Check((await repo.ResolveOperationAsync(OperationIdentity.Describe(apply), false, identity.Id, identity.WorkstationId, default)).Receipt == applied[0].Value, "Una respuesta perdida recupera el mismo recibo aplicado");
        var final = await Prepare(partial, [new(partial.Lines.Single().Id, 6)]); Saved(await service.ApplyAsync(await ApplyRequest(final)));
        Check((await Read(orderId)).Order.State == "RECIBIDA" && (await Read(orderId)).Lines.Single().Pending == 0, "Recepción completa deja pendiente cero y estado recibido");

        var competing = await Confirm(Create(freshProduct, 5, 7));
        var receiptA = await Prepare(competing, [new(competing.Lines.Single().Id, 4)]); competing = await Read(competing.Order.Id);
        var receiptB = await Prepare(competing, [new(competing.Lines.Single().Id, 4)]); competing = await Read(competing.Order.Id);
        var a = new PurchaseReceiptAction(Guid.NewGuid(), receiptA.Receipt.Id, receiptA.Receipt.RowVersion, competing.Order.RowVersion, true, "Equipo A");
        var b = new PurchaseReceiptAction(Guid.NewGuid(), receiptB.Receipt.Id, receiptB.Receipt.RowVersion, competing.Order.RowVersion, true, "Equipo B");
        var competition = await Task.WhenAll(service.ApplyAsync(a), service.ApplyAsync(b));
        Check(competition.Count(x => x.IsSuccess) == 1 && competition.Count(x => x.ErrorCode == "CONCURRENCY_CONFLICT") == 1, "Dos estaciones aplican contra misma OC: sólo una versión gana");
        var loser = competition[0].IsSuccess ? b : a;
        Check((await service.ApplyAsync(loser with { OperationId = Guid.NewGuid(), ExpectedOrderVersion = (await Read(competing.Order.Id)).Order.RowVersion })).ErrorCode == "CONCURRENCY_CONFLICT", "Actualizar versión no permite sobre-recepción del saldo pendiente");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p;", new { p = freshProduct }) == 4, "Inventario sin fila previa se crea una vez con cantidad correcta");
        var losingReceipt = (await service.ReceiptAsync(loser.ReceiptId))!; Saved(await service.ApplyAsync(await ApplyRequest(losingReceipt, false)));
        Check((await service.ReceiptAsync(loser.ReceiptId))!.Receipt.State == "ANULADA", "Anula borrador sin revertir stock aplicado por otro equipo");

        var batchOrder = await Confirm(Create(lotProduct, 5, 12)); var lineId = batchOrder.Lines.Single().Id;
        var today = await c.ExecuteScalarAsync<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        var expired = new PreparePurchaseReceipt(Guid.NewGuid(), batchOrder.Order.Id, batchOrder.Order.RowVersion, warehouse, 1, true, [new(lineId, 1, LotNumber: "VENCIDO", Expires: today.AddDays(-1))], "Vencido");
        var beforeExpired = await InventoryState(); Check((await service.PrepareAsync(expired)).ErrorCode == "VALIDATION" && await InventoryState() == beforeExpired, "Rechaza vencidos sin dejar borrador ni movimiento parcial");
        var batches = await Prepare(batchOrder, [new(lineId, 2, LotNumber: "BATCH-1", Expires: today.AddDays(30), Manufactured: today), new(lineId, 3, LotNumber: "BATCH-2", Expires: today.AddDays(60))]);
        Saved(await service.ApplyAsync(await ApplyRequest(batches)));
        Check(await c.ExecuteScalarAsync<decimal>("SELECT SUM(e.StockActual) FROM inv.ExistenciasLote e JOIN inv.LotesProducto l ON l.IdLote=e.IdLote WHERE l.IdProducto=@p;", new { p = lotProduct }) == 5, "Recepción con varios lotes concilia stock agregado y por lote");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.LotesProducto l JOIN cmp.RecepcionCompraDetalle d ON d.IdDetalle=l.IdRecepcionCompraDetalle WHERE d.IdRecepcion=@id AND (l.NumeroLote=N'BATCH-2' OR l.FechaFabricacion=@today);", new { id = batches.Receipt.Id, today }) == 2, "Lotes conservan vínculo a recepción, fabricación y vencimiento");
        var box = Saved(await new CashService(repo, session).SaveBoxAsync(new(Guid.NewGuid(), 0, "", "Caja FEFO", warehouse, true, "Prueba")));
        var fefo = await new SalesService(repo, session, gateway).QuoteAsync(new(checked((int)box), null, [new(lotProduct, 3)]));
        Check(fefo.Lines.First().Lots.First().Quantity == 2, "Lotes recibidos alimentan FEFO de ventas");

        var incompleteOrder = await Confirm(Create(p, 1, 15)); var incomplete = await Prepare(incompleteOrder, [new(incompleteOrder.Lines[0].Id, 1)], false);
        var beforeIncomplete = await InventoryState(); Check((await service.ApplyAsync(await ApplyRequest(incomplete))).ErrorCode == "INVALID_STATE" && await InventoryState() == beforeIncomplete, "Costo incompleto no pone unidades a la venta");
        Saved(await service.ApplyAsync(await ApplyRequest(incomplete, false)));
        var rollbackReceipt = await Prepare(await Read(incompleteOrder.Order.Id), [new(incompleteOrder.Lines[0].Id, 1)]);
        var rollbackRequest = await ApplyRequest(rollbackReceipt); var beforeRollback = await InventoryState(); var versionBefore = (await Read(incompleteOrder.Order.Id)).Order.RowVersion;
        await c.ExecuteAsync("ALTER TABLE sis.Bitacora WITH NOCHECK ADD CONSTRAINT CK_TestPurchaseAudit CHECK(Accion<>N'APLICAR_RECEPCION');");
        var auditFailed = false;
        try { await service.ApplyAsync(rollbackRequest); } catch (SqlException ex) when (ex.Number == 547) { auditFailed = true; }
        finally { await c.ExecuteAsync("ALTER TABLE sis.Bitacora DROP CONSTRAINT CK_TestPurchaseAudit;"); }
        Check(auditFailed && await InventoryState() == beforeRollback && (await Read(incompleteOrder.Order.Id)).Order.RowVersion.SequenceEqual(versionBefore)
            && (await service.ReceiptAsync(rollbackReceipt.Receipt.Id))!.Receipt.State == "BORRADOR", "Fallo de auditoría revierte stock, costo, Kardex, pendiente, versiones y ledger");
        Check((await service.ApplyAsync(rollbackRequest)).IsSuccess, "Tras rollback se puede confirmar la solicitud original una sola vez");

        var foreign = catalogs.Currencies.FirstOrDefault(x => x.Code != "USD");
        if (foreign is null) throw new InvalidOperationException("Falta moneda de prueba.");
        var fxOrder = await Confirm(Create(p, 2, 50, foreign.Id, .15m)); var fxReceipt = await Prepare(fxOrder, [new(fxOrder.Lines[0].Id, 2, .5m)], true, .14m);
        Check(fxReceipt.Receipt.ExchangeRate == .14m && fxReceipt.Lines.Single().PurchaseCostUsd == 7 && fxReceipt.Lines.Single().LandedUnitCost == 7.5m, "Tipo de cambio real histórico prevalece sobre el estimado");
        Saved(await service.ApplyAsync(await ApplyRequest(fxReceipt)));
        Check((await service.ReceiptAsync(fxReceipt.Receipt.Id))!.Lines.Single().LandedUnitCost == 7.5m, "Costo histórico de recepción permanece después de aplicar");
        Check((await service.SaveAsync(Create(p, 1, 1) with { EstimatedRate = 2 })).ErrorCode == "VALIDATION", "USD exige cambio igual a uno");
        var blocked = await Confirm(Create(p, 1, 1)); await c.ExecuteAsync("INSERT INTO cmp.Importaciones(IdOrdenCompra) VALUES(@id);", new { id = blocked.Order.Id });
        Check((await service.PrepareAsync(new(Guid.NewGuid(), blocked.Order.Id, blocked.Order.RowVersion, warehouse, 1, true, [new(blocked.Lines[0].Id, 1)], "Importación"))).ErrorCode == "INVALID_STATE", "Orden con importación espera costeo y flujo 2.1B");
        var historicalName = fxOrder.Lines[0].Name; await c.ExecuteAsync("UPDATE inv.Productos SET Nombre=N'Nombre posterior' WHERE IdProducto=@p;", new { p });
        Check((await Read(fxOrder.Order.Id)).Lines[0].Name == historicalName && (await service.ReceiptAsync(fxReceipt.Receipt.Id))!.Lines[0].Name == historicalName, "Cambiar catálogo no reescribe snapshot de OC ni recepción");
        var other = await c.ExecuteScalarAsync<int>("INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,Activo,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(N'purchase_denied',N'Sin permiso',N'no-login',1,0);");
        Check((await repo.SavePurchaseAsync(Create(p, 1, 1), other, identity.WorkstationId, default)).ErrorCode == "FORBIDDEN", "SQL rechaza nuevas compras sin permiso");
        Check((await repo.SavePurchaseAsync(Create(p, 1, 1), identity.Id, int.MaxValue, default)).ErrorCode == "FORBIDDEN", "SQL exige una estación activa");
        var denied = false; try { await repo.ReadPurchaseAsync(orderId, other, default); } catch (UnauthorizedAccessException) { denied = true; }
        Check(denied, "SQL protege la consulta de importes de compra");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMPRAS.GESTIONAR');");
        Check((await service.SaveAsync(Create(p, 1, 1))).ErrorCode == "FORBIDDEN", "Revocación de permisos en SQL se aplica aunque la sesión esté abierta");
        Check((await service.ApplyAsync(apply)).Value == applied[0].Value, "Permiso revocado no impide recuperar el recibo propio ya confirmado");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=1 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMPRAS.GESTIONAR');");
        var stateBeforeInstall = await InventoryState(); await DevelopmentDatabase.InstallAsync(database, Path.Combine(root, "database"));
        Check(await InventoryState() == stateBeforeInstall && (await Read(orderId)).Order.State == "RECIBIDA", "Reaplicar 017 conserva compras, recepciones y existencias");
        Console.WriteLine($"{checks} comprobaciones de compras SQL correctas.");
    }
}
