using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Domain.Rules;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string OrderColumns = "p.IdPedido Id,p.IdCliente CustomerId,p.ClienteSnapshot Customer,p.Contacto Contact,p.Telefono Phone,p.Direccion Address,p.Departamento Department,p.Municipio Municipality,p.Referencia Reference,p.FechaPrevista Scheduled,p.Responsable Responsible,p.IdResponsable ResponsibleUserId,p.CargoEntrega DeliveryCharge,p.IdVenta SaleId,p.Estado State,p.FechaEntrega Delivered,p.EntregadoPor DeliveredBy,p.RowVersion,v.SaldoPendiente Pending,CASE WHEN v.IdVenta IS NULL THEN N'SIN_VENTA' WHEN v.SaldoPendiente=0 THEN N'PAGADO' WHEN v.SaldoPendiente<v.Total THEN N'PARCIAL' ELSE N'PENDIENTE' END PaymentState";
    public Task<Result<MvpReceipt>> PlaceOrderAsync(PlaceOrder r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "CREAR_PEDIDO", r, userId, workstationId, ["VENTAS.VER", "VENTAS.CREAR"], async w =>
    {
        Check(r);
        var customer = await w.Scalar<string?>("SELECT NombreRazonSocial FROM cli.Clientes WITH(HOLDLOCK) WHERE IdCliente=@CustomerId AND Activo=1;", r);
        Require(customer is not null, "INVALID_STATE", "El cliente no está disponible.");
        if (r.ResponsibleUserId is { } responsible)
            Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.Usuarios WITH(HOLDLOCK) WHERE IdUsuario=@responsible AND Activo=1) THEN 1 ELSE 0 END);", new { responsible }), "VALIDATION", "El responsable no está activo.");
        var address = new CustomerAddressData
        {
            Address = r.Address,
            Department = r.Department,
            Municipality = r.Municipality,
            Reference = r.Reference
        };
        if (r.CustomerAddressId is { } addressId)
        {
            var saved = await w.Maybe<CustomerAddressData>("SELECT Direccion Address,Departamento Department,Municipio Municipality,Referencia Reference FROM cli.ClienteDirecciones WITH(HOLDLOCK) WHERE IdDireccion=@addressId AND IdCliente=@CustomerId AND Activo=1;", new { addressId, r.CustomerId });
            Require(saved is not null, "VALIDATION", "La dirección no pertenece al cliente o está inactiva.");
            address = saved!;
        }

        var id = await w.Scalar<long>("INSERT INTO ven.PedidosMvp(IdCliente,ClienteSnapshot,Contacto,Telefono,Direccion,Departamento,Municipio,Referencia,FechaPrevista,Responsable,IdResponsable,CargoEntrega) OUTPUT INSERTED.IdPedido VALUES(@CustomerId,@customer,@Contact,@Phone,@Address,@Department,@Municipality,@Reference,@Scheduled,@Responsible,@ResponsibleUserId,@DeliveryCharge);", new { r.CustomerId, customer, Contact = r.Contact.Trim(), Phone = r.Phone.Trim(), Address = address.Address.Trim(), address.Department, address.Municipality, address.Reference, r.Scheduled, r.Responsible, r.ResponsibleUserId, r.DeliveryCharge });
        foreach (var line in r.Items.OrderBy(i => i.ProductId))
        {
            var changed = await w.Execute("INSERT INTO ven.PedidoLineasMvp(IdPedido,IdProducto,SKU,Nombre,Cantidad) SELECT @id,p.IdProducto,p.SKU,p.Nombre,@Quantity FROM inv.Productos p WITH(HOLDLOCK) JOIN cat.UnidadesMedida u WITH(HOLDLOCK) ON u.IdUnidadMedida=p.IdUnidadMedida WHERE p.IdProducto=@ProductId AND p.Activo=1 AND p.ControlaSerie=0 AND (u.PermiteDecimales=1 OR FLOOR(@Quantity)=@Quantity);", new { id, line.ProductId, line.Quantity });
            Require(changed == 1, "VALIDATION", "Un producto o cantidad del pedido no es compatible con el POS.");
        }

        await OrderEvent(w, id, userId, "RECIBIDO", r.Reason);
        return id;
    }, ct);
    private static async Task<OrderData> LockOrder(Work w, long id, byte[] version)
    {
        var order = await w.Maybe<OrderData>("SELECT IdPedido Id,IdCliente CustomerId,IdVenta SaleId,Estado State,CargoEntrega DeliveryCharge,RowVersion FROM ven.PedidosMvp WITH(UPDLOCK,HOLDLOCK) WHERE IdPedido=@id;", new { id });
        Require(order is not null, "INVALID_STATE", "El pedido no existe.");
        Version(order!.RowVersion, version);
        return order;
    }

    private static Task OrderEvent(Work w, long id, int userId, string state, string reason) => w.Execute("INSERT INTO ven.PedidoEventosMvp(IdPedido,IdUsuario,Estado,Motivo) VALUES(@id,@userId,@state,@reason);", new { id, userId, state, reason });
    public Task<Result<MvpReceipt>> TransitionOrderAsync(OrderTransition r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "ESTADO_PEDIDO", r, userId, workstationId, ["VENTAS.VER", "VENTAS.CREAR"], async w =>
    {
        Check(r);
        var order = await LockOrder(w, r.OrderId, r.ExpectedVersion);
        Require(OrderRules.CanTransition(order.State, r.State), "INVALID_STATE", "La transición no está permitida para el estado actual.");
        if (r.State is "EN_RUTA" or "ENTREGADO")
            Require(order.SaleId is not null && await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId AND Estado=N'CONFIRMADA') THEN 1 ELSE 0 END);", new { order.SaleId }), "INVALID_STATE", "Vincula una venta confirmada antes de despachar o entregar el pedido.");
        await w.Execute("UPDATE ven.PedidosMvp SET Estado=@State,FechaEntrega=CASE WHEN @State=N'ENTREGADO' THEN SYSDATETIMEOFFSET() ELSE NULL END,EntregadoPor=CASE WHEN @State=N'ENTREGADO' THEN @userId ELSE NULL END WHERE IdPedido=@OrderId;", new { r.State, userId, r.OrderId });
        await OrderEvent(w, r.OrderId, userId, r.State, r.Reason);
        return r.OrderId;
    }, ct);
    public Task<Result<MvpReceipt>> LinkSaleAsync(LinkOrderSale r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "VINCULAR_PEDIDO_VENTA", r, userId, workstationId, ["VENTAS.VER", "VENTAS.CREAR"], async w =>
    {
        Check(r);
        await LinkOrderSaleCore(w, r, userId);
        return r.OrderId;
    }, ct);
    private static void LinkableOrder(OrderData order) => Require(order.State is not ("CANCELADO" or "ENTREGADO") && order.SaleId is null, "INVALID_STATE", "El pedido ya tiene venta o está finalizado.");
    private static async Task LinkOrderSaleCore(Work w, LinkOrderSale r, int userId)
    {
        var order = await LockOrder(w, r.OrderId, r.ExpectedVersion);
        LinkableOrder(order);
        var sale = await w.Maybe<SaleData>("SELECT IdVenta Id,IdCliente CustomerId,Estado State,CargoEntrega DeliveryCharge FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId;", r);
        Require(sale is not null && sale.State == "CONFIRMADA" && sale.CustomerId == order.CustomerId && sale.DeliveryCharge == order.DeliveryCharge, "VALIDATION", "La venta debe estar confirmada y corresponder al cliente y cargo de entrega.");
        Require(await w.Scalar<bool>("""
            SELECT CONVERT(bit,CASE WHEN NOT EXISTS(
              SELECT IdProducto,SUM(Cantidad) Cantidad FROM ven.VentaDetalle WHERE IdVenta=@SaleId GROUP BY IdProducto
              EXCEPT SELECT IdProducto,Cantidad FROM ven.PedidoLineasMvp WHERE IdPedido=@OrderId)
              AND NOT EXISTS(SELECT IdProducto,Cantidad FROM ven.PedidoLineasMvp WHERE IdPedido=@OrderId
              EXCEPT SELECT IdProducto,SUM(Cantidad) FROM ven.VentaDetalle WHERE IdVenta=@SaleId GROUP BY IdProducto)
              THEN 1 ELSE 0 END);
            """, r), "VALIDATION", "La venta no corresponde a los productos y cantidades del pedido.");
        await w.Execute("UPDATE ven.PedidosMvp SET IdVenta=@SaleId WHERE IdPedido=@OrderId;", r);
        await OrderEvent(w, r.OrderId, userId, order.State, r.Reason);
    }
    public Task<OrderSnapshot?> ReadOrderAsync(long id, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], async w =>
    {
        var order = await w.Maybe<OrderData>($"SELECT {OrderColumns} FROM ven.PedidosMvp p LEFT JOIN ven.Ventas v ON v.IdVenta=p.IdVenta WHERE p.IdPedido=@id;", new { id });
        if (order is null)
            return null;
        var lines = await w.List<OrderLineData>("SELECT IdProducto ProductId,SKU,Nombre Name,Cantidad Quantity FROM ven.PedidoLineasMvp WHERE IdPedido=@id ORDER BY IdProducto;", new { id });
        var events = await EventPage(w, id, new());
        return new OrderSnapshot(order, lines, events.Items, events.Total);
    }, ct);
    private static Task<MvpPage<OrderEventData>> EventPage(Work w, long id, MvpFilter f) => Page<OrderEventData>(w, "ven.PedidoEventosMvp", "Fecha Date,IdUsuario UserId,Estado State,Motivo Reason", "IdPedido=@id", "IdEvento DESC", new { id, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize });
    public Task<MvpPage<OrderEventData>> OrderEventsAsync(long id, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => EventPage(w, id, f), ct);
    public Task<MvpPage<OrderData>> OrdersAsync(string? state, MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["VENTAS.VER"], w => Page<OrderData>(w, "ven.PedidosMvp p LEFT JOIN ven.Ventas v ON v.IdVenta=p.IdVenta", OrderColumns, "(@state IS NULL OR p.Estado=@state)", "p.IdPedido DESC", new { state, Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
    public Task<Result<MvpReceipt>> InitialInventoryAsync(InitialInventory r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "SALDO_INICIAL", r, userId, workstationId, ["INVENTARIO.VER", "INVENTARIO.AJUSTAR"], async w =>
    {
        Check(r);
        await w.Execute("SELECT IdProducto FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;", r);
        Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId AND IdAlmacen=@WarehouseId) OR EXISTS(SELECT 1 FROM inv.MovimientosInventario WITH(HOLDLOCK) WHERE IdProducto=@ProductId AND IdAlmacen=@WarehouseId) THEN 1 ELSE 0 END);", r), "CONCURRENCY_CONFLICT", "Ya existe saldo o historial para ese producto y almacén. Consulta antes de ajustar.");
        return await w.Scalar<long>("EXEC inv.sp_RegistrarAjuste @ProductId=@ProductId,@WarehouseId=@WarehouseId,@Quantity=@Quantity,@UnitCost=@UnitCost,@Type=N'AJUSTE',@Reason=@Reason,@ExpectedVersion=0x,@NewLotNumber=@LotNumber,@Manufactured=@Manufactured,@Expires=@Expires,@UserId=@userId,@WorkstationId=@workstationId;", new { r.ProductId, r.WarehouseId, r.Quantity, r.UnitCost, r.Reason, r.LotNumber, r.Manufactured, r.Expires, userId, workstationId });
    }, ct);
    public Task<Result<MvpReceipt>> SaveBarcodeAsync(BarcodeChange r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "CODIGO_BARRAS", r, userId, workstationId, ["INVENTARIO.VER", "INVENTARIO.PRODUCTOS_GESTIONAR"], async w =>
    {
        Check(r);
        var version = await w.Scalar<byte[]?>("SELECT RowVersion FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;", r);
        Version(version, r.ExpectedVersion);
        var generated = string.IsNullOrWhiteSpace(r.Code);
        Require(!generated || r.Type == "CODE128", "VALIDATION", "El código interno utiliza CODE128.");
        var code = generated ? $"GD{r.ProductId:D10}" : r.Code!.Trim();
        var existing = await w.Scalar<long?>("SELECT IdCodigoBarra FROM inv.ProductoCodigosBarra WITH(UPDLOCK,HOLDLOCK) WHERE Codigo=@code AND IdProducto=@ProductId;", new { code, r.ProductId });
        var id = existing ?? await w.Scalar<long>("INSERT INTO inv.ProductoCodigosBarra(IdProducto,Codigo,Tipo,EsPrincipal,GeneradoInternamente,Activo) OUTPUT INSERTED.IdCodigoBarra VALUES(@ProductId,@code,@Type,0,@generated,@Active);", new { r.ProductId, code, r.Type, generated, r.Active });
        if (existing is not null)
            await w.Execute("UPDATE inv.ProductoCodigosBarra SET Activo=@Active WHERE IdCodigoBarra=@id;", new { id, r.Active });
        await w.Execute("UPDATE inv.Productos SET Nombre=Nombre WHERE IdProducto=@ProductId;", r);
        return id;
    }, ct);
    public Task<Result<MvpReceipt>> RegisterPendingAsync(PendingFiscal r, int userId, int? workstationId, CancellationToken ct) => Write(r.OperationId, "FISCAL_PENDIENTE", r, userId, workstationId, ["FISCAL.VER", "FISCAL.CONFIGURAR", "VENTAS.VER"], async w =>
    {
        Check(r);
        var sale = await w.Maybe<SaleData>("SELECT IdVenta Id,IdCliente CustomerId,Estado State FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId;", r);
        Require(sale?.State == "CONFIRMADA", "INVALID_STATE", "El documento requiere una venta confirmada.");
        if (r.Type == "CCFE")
            Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cli.Clientes c WITH(HOLDLOCK) WHERE c.IdCliente=@customerId AND NULLIF(LTRIM(RTRIM(c.NIT)),N'') IS NOT NULL AND NULLIF(LTRIM(RTRIM(c.NRC)),N'') IS NOT NULL AND EXISTS(SELECT 1 FROM cli.ClienteDirecciones d WITH(HOLDLOCK) WHERE d.IdCliente=c.IdCliente AND d.Activo=1 AND d.Tipo=N'FISCAL')) THEN 1 ELSE 0 END);", new { customerId = sale!.CustomerId }), "VALIDATION", "Completa NIT, NRC y dirección fiscal antes de preparar crédito fiscal.");
        Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM fis.DocumentosFiscales WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId AND Estado<>N'ANULADO' AND TipoDocumento<>@Type) THEN 1 ELSE 0 END);", r), "INVALID_STATE", "La venta ya tiene un documento fiscal activo de otro tipo.");
        var existing = await w.Scalar<long?>("SELECT TOP(1) IdDocumentoFiscal FROM fis.DocumentosFiscales WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@SaleId AND TipoDocumento=@Type AND Estado<>N'ANULADO' ORDER BY IdDocumentoFiscal;", r);
        return existing ?? await w.Scalar<long>("INSERT INTO fis.DocumentosFiscales(IdVenta,TipoDocumento,Estado) OUTPUT INSERTED.IdDocumentoFiscal VALUES(@SaleId,@Type,N'PENDIENTE');", r);
    }, ct);
    public Task<MvpPage<PendingFiscalData>> PendingAsync(MvpFilter f, int userId, CancellationToken ct) => Read(userId, ["FISCAL.VER"], w => Page<PendingFiscalData>(w, "fis.DocumentosFiscales", "IdDocumentoFiscal Id,IdVenta SaleId,TipoDocumento Type,Estado State,Fecha Date", "Estado=N'PENDIENTE'", "IdDocumentoFiscal DESC", new { Offset = checked((f.Page - 1) * f.PageSize), f.PageSize }), ct);
}
