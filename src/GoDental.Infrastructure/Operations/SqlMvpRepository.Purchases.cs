using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string PurchaseColumns = "o.IdOrdenCompra Id,o.NumeroOrden Number,o.IdProveedor SupplierId,p.Nombre Supplier,o.IdMoneda CurrencyId,m.CodigoISO Currency,ISNULL(o.TipoCambioEstimado,0) EstimatedRate,o.FechaOrden Date,o.Estado State,o.Subtotal,o.Total,o.RowVersion";
    private const string PurchaseFrom = "cmp.OrdenesCompra o JOIN cmp.Proveedores p ON p.IdProveedor=o.IdProveedor JOIN cat.Monedas m ON m.IdMoneda=o.IdMoneda";
    private const string PurchaseLineColumns = "d.IdDetalle Id,d.IdProducto ProductId,ISNULL(d.SKU,p.SKU) Sku,ISNULL(d.Nombre,p.Nombre) Name,d.Cantidad Quantity,d.CantidadRecibida Received,d.CostoUnitario UnitCost,d.Descuento Discount,d.TotalLinea Total,p.ControlaLote TracksLot,p.ControlaVencimiento TracksExpiry,d.CostoImportacionUSD ImportPurchaseCostUsd,d.GastoImportacionUSD ImportExpenseUsd";
    private const string ReceiptColumns = "r.IdRecepcion Id,r.NumeroRecepcion Number,r.IdOrdenCompra OrderId,r.IdImportacion ImportId,r.IdAlmacen WarehouseId,a.Nombre Warehouse,r.FechaRecepcion Date,r.Estado State,r.TipoCambioReal ExchangeRate,r.CostosCompletos CostsComplete,u.NombreCompleto [User],r.RowVersion";
    private const string ReceiptFrom = "cmp.RecepcionesCompra r JOIN inv.Almacenes a ON a.IdAlmacen=r.IdAlmacen JOIN seg.Usuarios u ON u.IdUsuario=r.RecibidoPor";
    private sealed class ReceivingProduct
    {
        public int Id { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public bool TracksLot { get; set; }
        public bool TracksExpiry { get; set; }
        public bool Decimals { get; set; }
        public bool Serial { get; set; }
    }
    private sealed class ReceivingStock { public decimal Quantity { get; set; } public decimal Cost { get; set; } }
    private static void CheckPurchase(object r)
    {
        if (PurchaseValidator.Validate(r) is { } error) throw new RuleFailure("VALIDATION", error);
    }
    private static async Task Station(Work w, int? workstationId)
        => Require(workstationId is not null && await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM sis.EquiposTrabajo WITH(HOLDLOCK) WHERE IdEquipo=@workstationId AND Activo=1) THEN 1 ELSE 0 END);", new { workstationId }), "FORBIDDEN", "La estación de trabajo no está activa.");
    private static async Task<ReceivingProduct> ReceivingItem(Work w, int id)
    {
        var product = await w.Maybe<ReceivingProduct>("SELECT p.IdProducto Id,p.SKU,p.Nombre Name,p.ControlaLote TracksLot,p.ControlaVencimiento TracksExpiry,p.ControlaSerie Serial,u.PermiteDecimales Decimals FROM inv.Productos p WITH(UPDLOCK,HOLDLOCK) JOIN cat.UnidadesMedida u WITH(HOLDLOCK) ON u.IdUnidadMedida=p.IdUnidadMedida WHERE p.IdProducto=@id AND p.Activo=1 AND u.Activo=1;", new { id });
        Require(product is not null && !product.Serial, "INVALID_STATE", "El producto está inactivo o requiere control de series todavía no implementado.");
        return product!;
    }
    private static void PurchaseQuantity(ReceivingProduct product, decimal quantity)
        => Require(product.Decimals || decimal.Truncate(quantity) == quantity, "VALIDATION", "La unidad no admite cantidades fraccionarias.");
    private static async Task CurrencyRate(Work w, short currencyId, decimal rate)
    {
        var currency = await w.Scalar<string?>("SELECT CodigoISO FROM cat.Monedas WITH(HOLDLOCK) WHERE IdMoneda=@currencyId AND Activo=1;", new { currencyId });
        Require(currency is not null && PurchaseValidator.Rate(rate) && (currency != "USD" || rate == 1), "VALIDATION", "Selecciona una moneda activa y el cambio USD por unidad. Para USD debe ser 1.");
    }
    private static Task<PurchaseOrderData?> Purchase(Work w, long id, bool locking = false) => w.Maybe<PurchaseOrderData>($"SELECT {PurchaseColumns} FROM cmp.OrdenesCompra o {(locking ? "WITH(UPDLOCK,HOLDLOCK)" : "WITH(HOLDLOCK)")} JOIN cmp.Proveedores p ON p.IdProveedor=o.IdProveedor JOIN cat.Monedas m ON m.IdMoneda=o.IdMoneda WHERE o.IdOrdenCompra=@id;", new { id });
    private static Task<IReadOnlyList<PurchaseOrderLineData>> PurchaseLines(Work w, long id) => w.List<PurchaseOrderLineData>($"SELECT {PurchaseLineColumns} FROM cmp.OrdenCompraDetalle d WITH(HOLDLOCK) JOIN inv.Productos p ON p.IdProducto=d.IdProducto WHERE d.IdOrdenCompra=@id ORDER BY d.IdProducto,d.IdDetalle;", new { id });
    private static async Task<PurchaseOrderData> LockPurchase(Work w, long id, byte[] version)
    {
        var order = await Purchase(w, id, true); Version(order?.RowVersion, version); return order!;
    }
    private static Task TouchPurchase(Work w, long id) => w.Execute("UPDATE cmp.OrdenesCompra SET Observaciones=Observaciones WHERE IdOrdenCompra=@id;", new { id });
    private static async Task NoImport(Work w, long id)
        => Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.Importaciones WITH(UPDLOCK,HOLDLOCK) WHERE IdOrdenCompra=@id) THEN 1 ELSE 0 END);", new { id }), "INVALID_STATE", "La orden tiene importación. Su recepción y prorrateo requieren el bloque 2.1B.");
    public Task<PurchaseCatalogs> PurchaseCatalogsAsync(int userId, CancellationToken ct) => Read(userId, ["COMPRAS.VER"], async w => new PurchaseCatalogs(
        await w.List<CatalogOption>("SELECT IdProveedor Id,CONCAT(Codigo,N' · ',Nombre) Name FROM cmp.Proveedores WHERE Activo=1 ORDER BY Nombre,IdProveedor;"),
        await w.List<PurchaseCurrency>("SELECT IdMoneda Id,CodigoISO Code,CONCAT(CodigoISO,N' · ',Nombre) Name FROM cat.Monedas WHERE Activo=1 ORDER BY EsBase DESC,CodigoISO;"),
        await w.List<CatalogOption>("SELECT IdAlmacen Id,Nombre Name FROM inv.Almacenes WHERE Activo=1 ORDER BY EsPrincipal DESC,Nombre;")), ct);
    public Task<MvpPage<PurchaseOrderData>> PurchaseOrdersAsync(string search, string? state, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["COMPRAS.VER"], w => Page<PurchaseOrderData>(w, PurchaseFrom, PurchaseColumns,
        "(@state IS NULL OR o.Estado=@state) AND (@search=N'' OR CHARINDEX(@search,o.NumeroOrden)>0 OR CHARINDEX(@search,p.Nombre)>0)", "o.IdOrdenCompra DESC", new { state, search, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<MvpPage<ProductData>> PurchaseProductsAsync(string search, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["COMPRAS.VER"], w => Page<ProductData>(w, "inv.Productos p", "p.IdProducto Id,p.SKU,p.Nombre Name,p.ControlaLote TracksLot,p.ControlaVencimiento TracksExpiry,p.Activo Active",
        "p.Activo=1 AND p.ControlaSerie=0 AND (@search=N'' OR CHARINDEX(@search,p.SKU)>0 OR CHARINDEX(@search,p.Nombre)>0)", "p.Nombre,p.IdProducto", new { search, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<PurchaseOrderSnapshot?> ReadPurchaseAsync(long id, int userId, CancellationToken ct) => Read(userId, ["COMPRAS.VER"], async w =>
    {
        var order = await Purchase(w, id); if (order is null) return null;
        return new PurchaseOrderSnapshot(order, await PurchaseLines(w, id), await w.List<PurchaseReceiptData>($"SELECT {ReceiptColumns} FROM {ReceiptFrom} WHERE r.IdOrdenCompra=@id ORDER BY r.IdRecepcion DESC;", new { id }));
    }, ct);
    private static Task<IReadOnlyList<PurchaseReceiptLineData>> ReceiptLines(Work w, long id) => w.List<PurchaseReceiptLineData>("SELECT r.IdDetalle Id,r.IdOrdenCompraDetalle OrderLineId,r.IdProducto ProductId,ISNULL(d.SKU,p.SKU) Sku,ISNULL(d.Nombre,p.Nombre) Name,r.CantidadRecibida Quantity,r.CostoCompraUSD PurchaseCostUsd,r.GastoAsignadoUSD ExtraUnitCostUsd,r.CostoPuestoBodega LandedUnitCost,r.NumeroLote LotNumber,r.FechaVencimiento Expires,r.FechaFabricacion Manufactured,r.GastoImportacionTotalUSD ImportExpenseTotalUsd FROM cmp.RecepcionCompraDetalle r JOIN cmp.OrdenCompraDetalle d ON d.IdDetalle=r.IdOrdenCompraDetalle JOIN inv.Productos p ON p.IdProducto=r.IdProducto WHERE r.IdRecepcion=@id ORDER BY r.IdProducto,r.IdDetalle;", new { id });
    public Task<PurchaseReceiptSnapshot?> ReadPurchaseReceiptAsync(long id, int userId, CancellationToken ct) => Read(userId, ["COMPRAS.VER"], async w =>
    {
        var receipt = await w.Maybe<PurchaseReceiptData>($"SELECT {ReceiptColumns} FROM {ReceiptFrom} WHERE r.IdRecepcion=@id;", new { id });
        return receipt is null ? null : new PurchaseReceiptSnapshot(receipt, await ReceiptLines(w, id));
    }, ct);
    public Task<Result<MvpReceipt>> SavePurchaseAsync(SavePurchaseOrder r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "GUARDAR_OC", r, userId, workstationId, ["COMPRAS.VER", "COMPRAS.GESTIONAR"], async w =>
    {
        CheckPurchase(r); await Station(w, workstationId);
        if (r.Id > 0) Require((await LockPurchase(w, r.Id, r.ExpectedVersion)).State == "BORRADOR", "INVALID_STATE", "Sólo se edita una orden en borrador.");
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.Proveedores WITH(HOLDLOCK) WHERE IdProveedor=@SupplierId AND Activo=1) THEN 1 ELSE 0 END);", r), "INVALID_STATE", "El proveedor no está activo.");
        await CurrencyRate(w, r.CurrencyId, r.EstimatedRate);
        var items = new List<(PurchaseLine Line, ReceivingProduct Product)>();
        foreach (var line in r.Lines.OrderBy(l => l.ProductId)) { var product = await ReceivingItem(w, line.ProductId); PurchaseQuantity(product, line.Quantity); items.Add((line, product)); }
        var subtotal = items.Sum(i => PurchaseRules.Money(i.Line.Quantity * i.Line.UnitCost));
        var total = items.Sum(i => PurchaseRules.Money(i.Line.Quantity * i.Line.UnitCost - i.Line.Discount));
        Require(MvpValidator.Money(subtotal) && MvpValidator.Money(total), "VALIDATION", "El total supera el rango monetario.");
        var id = r.Id;
        if (id == 0) id = await w.Scalar<long>("INSERT INTO cmp.OrdenesCompra(NumeroOrden,IdProveedor,IdMoneda,TipoCambioEstimado,FechaOrden,Subtotal,Total,CreadoPor,Observaciones) OUTPUT INSERTED.IdOrdenCompra VALUES(@number,@SupplierId,@CurrencyId,@EstimatedRate,@Date,@subtotal,@total,@userId,@Reason);", new { number = await Number(w, "ORDEN_COMPRA"), r.SupplierId, r.CurrencyId, r.EstimatedRate, r.Date, subtotal, total, userId, r.Reason });
        else
        {
            Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.RecepcionesCompra WITH(HOLDLOCK) WHERE IdOrdenCompra=@id) THEN 1 ELSE 0 END);", new { id }), "INVALID_STATE", "La orden ya tiene recepciones y no se modifica.");
            await w.Execute("DELETE FROM cmp.OrdenCompraDetalle WHERE IdOrdenCompra=@id;UPDATE cmp.OrdenesCompra SET IdProveedor=@SupplierId,IdMoneda=@CurrencyId,TipoCambioEstimado=@EstimatedRate,FechaOrden=@Date,Subtotal=@subtotal,Total=@total,Observaciones=@Reason WHERE IdOrdenCompra=@id;", new { id, r.SupplierId, r.CurrencyId, r.EstimatedRate, r.Date, subtotal, total, r.Reason });
        }
        foreach (var (line, product) in items)
            await w.Execute("INSERT INTO cmp.OrdenCompraDetalle(IdOrdenCompra,IdProducto,Cantidad,CostoUnitario,Descuento,TotalLinea,SKU,Nombre) VALUES(@id,@ProductId,@Quantity,@UnitCost,@Discount,@total,@Sku,@Name);", new { id, line.ProductId, line.Quantity, line.UnitCost, line.Discount, total = PurchaseRules.Money(line.Quantity * line.UnitCost - line.Discount), product.Sku, product.Name });
        return id;
    }, ct);
    public Task<Result<MvpReceipt>> TransitionPurchaseAsync(PurchaseTransition r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "ESTADO_OC", r, userId, workstationId, ["COMPRAS.VER", "COMPRAS.GESTIONAR"], async w =>
    {
        CheckPurchase(r); await Station(w, workstationId);
        var order = await LockPurchase(w, r.OrderId, r.ExpectedVersion);
        await NoImport(w,order.Id);
        var lines = await PurchaseLines(w, r.OrderId);
        Require(lines.Count > 0 && PurchaseRules.CanTransition(order.State, r.State, lines.Any(l => l.Received > 0)), "INVALID_STATE", "La transición no está permitida o ya hay unidades recibidas.");
        await w.Execute("UPDATE cmp.OrdenesCompra SET Estado=@State WHERE IdOrdenCompra=@OrderId;", r);
        return r.OrderId;
    }, ct);
    public Task<Result<MvpReceipt>> PrepareReceiptAsync(PreparePurchaseReceipt r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "PREPARAR_RECEPCION", r, userId, workstationId, ["COMPRAS.VER", "COMPRAS.GESTIONAR"], async w =>
    {
        CheckPurchase(r); await Station(w,workstationId);
        var order=await LockPurchase(w,r.OrderId,r.ExpectedOrderVersion); await NoImport(w,order.Id);
        return await PrepareReceiptCore(w,r,order,userId,null);
    },ct);
    private static async Task<long> PrepareReceiptCore(Work w,PreparePurchaseReceipt r,PurchaseOrderData order,int userId,ImportationData? import)
    {
        Require(PurchaseRules.CanReceive(order.State),"INVALID_STATE","Confirma la orden antes de recibir.");
        await CurrencyRate(w,order.CurrencyId,r.ExchangeRate);
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1) THEN 1 ELSE 0 END);",r),"INVALID_STATE","El almacén no está activo.");
        var lines=await PurchaseLines(w,order.Id);ValidatePending(lines,r.Lines.Select(l=>(l.OrderLineId,l.Quantity)).ToArray());
        var incoming=r.Lines.OrderBy(l=>lines.Single(d=>d.Id==l.OrderLineId).ProductId).ThenBy(l=>l.OrderLineId).ToArray();
        var allocations=import is null ? null : await ReceiptExpenses(w,lines,incoming);
        var today=await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        var receipt=await w.Scalar<long>("INSERT INTO cmp.RecepcionesCompra(NumeroRecepcion,IdOrdenCompra,IdImportacion,IdAlmacen,RecibidoPor,Observaciones,TipoCambioReal,CostosCompletos) OUTPUT INSERTED.IdRecepcion VALUES(@number,@OrderId,@importId,@WarehouseId,@userId,@Reason,@ExchangeRate,@CostsComplete);",new{number=await Number(w,"RECEPCION_COMPRA"),r.OrderId,importId=import?.Id,r.WarehouseId,userId,r.Reason,r.ExchangeRate,r.CostsComplete});
        for(var index=0;index<incoming.Length;index++)
        {
            var item=incoming[index];var original=lines.Single(l=>l.Id==item.OrderLineId);var product=await ReceivingItem(w,original.ProductId);
            PurchaseQuantity(product,item.Quantity);await LotForReceipt(w,product,item.LotNumber,item.Manufactured,item.Expires,today);
            decimal? allocated=allocations is null?null:allocations[index];
            var purchase=import is null?PurchaseRules.PurchaseCostUsd(original.UnitCost,original.Discount,original.Quantity,r.ExchangeRate):original.ImportPurchaseCostUsd!.Value;
            var extra=allocated is {} amount?PurchaseRules.Cost(amount/item.Quantity):item.ExtraUnitCostUsd;var landed=purchase+extra;
            Require(PurchaseValidator.Cost(purchase)&&PurchaseValidator.Cost(landed),"VALIDATION","El costo convertido supera el rango unitario.");
            await w.Execute("INSERT INTO cmp.RecepcionCompraDetalle(IdRecepcion,IdOrdenCompraDetalle,IdProducto,CantidadRecibida,CostoCompraUSD,GastoAsignadoUSD,CostoPuestoBodega,NumeroLote,FechaVencimiento,FechaFabricacion,GastoImportacionTotalUSD) VALUES(@receipt,@OrderLineId,@ProductId,@Quantity,@purchase,@extra,@landed,@LotNumber,@Expires,@Manufactured,@allocated);",new{receipt,item.OrderLineId,original.ProductId,item.Quantity,purchase,extra,landed,item.LotNumber,item.Expires,item.Manufactured,allocated});
        }
        if(import is not null){await TouchImport(w,import.Id);await ImportNote(w,import.Id,"BORRADOR_RECEPCION",$"Recepción #{receipt} preparada; no ingresa stock.",userId);}
        await TouchPurchase(w,order.Id);return receipt;
    }
    private static void ValidatePending(IReadOnlyList<PurchaseOrderLineData> lines, IReadOnlyList<(long Id, decimal Quantity)> received)
    {
        foreach (var group in received.GroupBy(l => l.Id))
        {
            var line = lines.SingleOrDefault(l => l.Id == group.Key);
            Require(line is not null && group.Sum(l => l.Quantity) <= line.Pending, "CONCURRENCY_CONFLICT", "La cantidad recibida supera el pendiente o no corresponde a la orden. Actualiza el detalle.");
        }
    }
    private static async Task LotForReceipt(Work w, ReceivingProduct product, string? lot, DateTime? manufactured, DateTime? expires, DateTime today)
    {
        if (!product.TracksLot) { Require(string.IsNullOrWhiteSpace(lot) && manufactured is null && expires is null, "VALIDATION", "El producto no controla lotes."); return; }
        Require(!string.IsNullOrWhiteSpace(lot) && lot.Length <= 100 && (!product.TracksExpiry || expires is not null) && !(manufactured > expires) && !(expires < today), "VALIDATION", "Revisa número de lote, fabricación y vencimiento. No se recibe mercadería vencida.");
        Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.LotesProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@id AND NumeroLote=@lot AND Activo=0) THEN 1 ELSE 0 END);", new { id = product.Id, lot }), "INVALID_STATE", "Ese lote está bloqueado; no se recibe como vendible.");
    }
    public Task<Result<MvpReceipt>> ActOnReceiptAsync(PurchaseReceiptAction r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, r.Apply ? "APLICAR_RECEPCION" : "ANULAR_RECEPCION", r, userId, workstationId, ["COMPRAS.VER", "COMPRAS.GESTIONAR"], async w =>
    {
        CheckPurchase(r); await Station(w, workstationId);
        var orderId = await w.Scalar<long?>("SELECT IdOrdenCompra FROM cmp.RecepcionesCompra WHERE IdRecepcion=@ReceiptId;", r);
        Require(orderId is not null, "INVALID_STATE", "La recepción no existe.");
        var order = await LockPurchase(w, orderId!.Value, r.ExpectedOrderVersion);
        var import=await Importation(w,order.Id);
        var receipt = await w.Maybe<PurchaseReceiptData>($"SELECT {ReceiptColumns} FROM cmp.RecepcionesCompra r WITH(UPDLOCK,HOLDLOCK) JOIN inv.Almacenes a ON a.IdAlmacen=r.IdAlmacen JOIN seg.Usuarios u ON u.IdUsuario=r.RecibidoPor WHERE r.IdRecepcion=@ReceiptId;", r);
        Version(receipt?.RowVersion, r.ExpectedVersion);
        Require(receipt!.State == "BORRADOR", "INVALID_STATE", "Sólo se aplica o anula un borrador; una recepción aplicada no se revierte aquí.");
        Require(receipt.OrderId == order.Id, "INVALID_STATE", "La recepción no corresponde a la orden consultada.");
        Require(receipt.ImportId==import?.Id,"INVALID_STATE","El origen de la recepción no coincide con la importación de la orden.");
        if (!r.Apply) { await w.Execute("UPDATE cmp.RecepcionesCompra SET Estado=N'ANULADA' WHERE IdRecepcion=@ReceiptId;", r); await TouchPurchase(w, order.Id); if(import is not null) { await TouchImport(w,import.Id); await ImportNote(w,import.Id,"ANULAR_RECEPCION",$"Borrador #{receipt.Id} anulado.",userId); } return r.ReceiptId; }
        Require(PurchaseRules.CanReceive(order.State) && receipt.CostsComplete && PurchaseValidator.Rate(receipt.ExchangeRate ?? 0), "INVALID_STATE", "La orden no admite recepción o el costo no se confirmó completo. Corrige el borrador antes de aplicar.");
        await CurrencyRate(w, order.CurrencyId, receipt.ExchangeRate!.Value);
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.Almacenes WITH(HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND Activo=1) THEN 1 ELSE 0 END);", receipt), "INVALID_STATE", "El almacén está inactivo.");
        var lines = await ReceiptLines(w, receipt.Id); var ordered = await PurchaseLines(w, order.Id);
        Require(lines.Count is > 0 and <= 100, "INVALID_STATE", "La recepción no contiene líneas válidas.");
        ValidatePending(ordered, lines.Select(l => (l.OrderLineId, l.Quantity)).ToArray());
        IReadOnlyDictionary<int,decimal>? allocations=null;
        if(import is not null)
        {
            Require(import.CostsClosed && import.Rate==receipt.ExchangeRate,"INVALID_STATE","La importación no tiene costos cerrados compatibles.");
            allocations=await ReceiptExpenses(w,ordered,lines.Select(l=>new ReceivePurchaseLine(l.OrderLineId,l.Quantity)).ToArray());
        }
        var today = await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        for(var index=0;index<lines.Count;index++)
        {
            var item=lines[index];
            var source = ordered.Single(l => l.Id == item.OrderLineId);
            Require(source.ProductId == item.ProductId && MvpValidator.Quantity(item.Quantity), "INVALID_STATE", "El detalle no corresponde al producto o cantidad de la orden.");
            var product = await ReceivingItem(w, item.ProductId); PurchaseQuantity(product, item.Quantity);
            await LotForReceipt(w, product, item.LotNumber, item.Manufactured, item.Expires, today);
            var calculated = import is null ? PurchaseRules.PurchaseCostUsd(source.UnitCost, source.Discount, source.Quantity, receipt.ExchangeRate!.Value) : source.ImportPurchaseCostUsd ?? -1;
            if(allocations is not null) Require(item.ImportExpenseTotalUsd==allocations[index] && item.ExtraUnitCostUsd==PurchaseRules.Cost(allocations[index]/item.Quantity),"CONCURRENCY_CONFLICT","El reparto del borrador cambió por otra recepción. Anula y prepara otro con el pendiente actualizado.");
            Require(PurchaseValidator.Cost(item.ExtraUnitCostUsd) && PurchaseValidator.Cost(item.LandedUnitCost) && calculated == item.PurchaseCostUsd && item.LandedUnitCost == calculated + item.ExtraUnitCostUsd, "INVALID_STATE", "Los costos no coinciden con la orden y su cambio histórico.");
            var stock = await w.Maybe<ReceivingStock>("SELECT StockActual Quantity,CostoPromedio Cost FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId AND IdAlmacen=@warehouse;", new { item.ProductId, warehouse = receipt.WarehouseId });
            var before = stock?.Quantity ?? 0; var after = before + item.Quantity;
            Require(after <= 99999999999999.9999m && before >= 0, "VALIDATION", "El saldo supera el rango de inventario.");
            var average = PurchaseRules.Average(before, stock?.Cost ?? 0, item.Quantity, item.LandedUnitCost);
            if (stock is null) await w.Execute("INSERT INTO inv.ExistenciasProducto(IdAlmacen,IdProducto,StockActual,CostoPromedio,FechaUltimoMovimiento) VALUES(@warehouse,@ProductId,@after,@average,SYSDATETIMEOFFSET());", new { warehouse = receipt.WarehouseId, item.ProductId, after, average });
            else await w.Execute("UPDATE inv.ExistenciasProducto SET StockActual=@after,CostoPromedio=@average,FechaUltimoMovimiento=SYSDATETIMEOFFSET() WHERE IdAlmacen=@warehouse AND IdProducto=@ProductId;", new { warehouse = receipt.WarehouseId, item.ProductId, after, average });
            long? lot = null;
            if (product.TracksLot)
            {
                lot = await w.Scalar<long>("INSERT INTO inv.LotesProducto(IdProducto,NumeroLote,FechaFabricacion,FechaVencimiento,CostoUnitarioIngreso,IdRecepcionCompraDetalle) OUTPUT INSERTED.IdLote VALUES(@ProductId,@LotNumber,@Manufactured,@Expires,@LandedUnitCost,@Id);", item);
                await w.Execute("INSERT INTO inv.ExistenciasLote(IdAlmacen,IdLote,StockActual) VALUES(@warehouse,@lot,@Quantity);", new { warehouse = receipt.WarehouseId, lot, item.Quantity });
            }
            await w.Execute("INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,ReferenciaTipo,ReferenciaId,IdUsuario,Observacion) VALUES(@warehouse,@ProductId,@lot,N'COMPRA',@Quantity,@LandedUnitCost,@before,@after,N'RECEPCION_COMPRA',@receiptId,@userId,@Reason);", new { warehouse = receipt.WarehouseId, item.ProductId, lot, item.Quantity, item.LandedUnitCost, before, after, receiptId = receipt.Id, userId, r.Reason });
            await w.Execute("UPDATE cmp.OrdenCompraDetalle SET CantidadRecibida=CantidadRecibida+@Quantity WHERE IdDetalle=@OrderLineId;", item);
        }
        await w.Execute("UPDATE cmp.RecepcionesCompra SET Estado=N'APLICADA' WHERE IdRecepcion=@ReceiptId;", r);
        await w.Execute("UPDATE cmp.OrdenesCompra SET Estado=CASE WHEN EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle WHERE IdOrdenCompra=@id AND CantidadRecibida<Cantidad) THEN N'RECIBIDA_PARCIAL' ELSE N'RECIBIDA' END WHERE IdOrdenCompra=@id;", new { id = order.Id });
        if(import is not null)
        {
            var partial=await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle WHERE IdOrdenCompra=@id AND CantidadRecibida<Cantidad) THEN 1 ELSE 0 END);",new{id=order.Id});
            await w.Execute("UPDATE cmp.Importaciones SET Estado=@state,FechaRecepcion=CASE WHEN @state=N'RECIBIDA' THEN @today ELSE FechaRecepcion END WHERE IdImportacion=@id;",new{state=partial?"RECIBIDA_PARCIAL":"RECIBIDA",today,id=import.Id});
            await ImportNote(w,import.Id,partial?"RECIBIDA_PARCIAL":"RECIBIDA",$"Recepción {receipt.Number} aplicada con costos cerrados.",userId);
        }
        return receipt.Id;
    }, ct);
}
