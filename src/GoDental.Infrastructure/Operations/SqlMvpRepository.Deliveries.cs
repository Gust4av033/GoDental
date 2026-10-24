using System.Text.Json;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string DeliveryColumns="IdReparto Id,IdPedido OrderId,IdCaja CashBoxId,IdAlmacen WarehouseId,Responsable Courier,Estado State,Total,SnapshotCalculo Snapshot,ClienteSnapshot CustomerSnapshot,IdVenta SaleId,Medio Medium,Referencia Reference,FechaSalida Dispatched,FechaEntrega Delivered,FechaRendicion Settled,RowVersion";
    public Task<DeliveryData?> DeliveryAsync(long orderId,int userId,CancellationToken ct)=>Read(userId,["VENTAS.VER"],w=>w.Maybe<DeliveryData>($"SELECT {DeliveryColumns} FROM ven.RepartosMvp WHERE IdPedido=@orderId;",new{orderId}),ct);
    private static async Task<SaleCart> DeliveryCart(Work w,OrderData order,int box)
    {
        Require(order.State=="LISTO"&&order.SaleId is null,"INVALID_STATE","Reparto requiere un pedido LISTO sin venta.");
        Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.RepartosMvp WITH(HOLDLOCK) WHERE IdPedido=@Id) THEN 1 ELSE 0 END);",order),"INVALID_STATE","El pedido ya tiene un reparto; consulta su resolución.");
        var lines=await w.List<OrderLineData>("SELECT IdProducto ProductId,Cantidad Quantity FROM ven.PedidoLineasMvp WITH(HOLDLOCK) WHERE IdPedido=@Id ORDER BY IdProducto;",order);
        return new(box,order.CustomerId,lines.Select(l=>new SaleItem(l.ProductId,l.Quantity)).ToArray(),DeliveryCharge:order.DeliveryCharge);
    }
    public Task<SaleQuote> QuoteDeliveryAsync(long orderId,int cashBoxId,int userId,CancellationToken ct)=>Read(userId,["VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR"],async w=>
    {
        var order=await w.Maybe<OrderData>("SELECT IdPedido Id,IdCliente CustomerId,IdVenta SaleId,Estado State,CargoEntrega DeliveryCharge FROM ven.PedidosMvp WITH(UPDLOCK,HOLDLOCK) WHERE IdPedido=@orderId;",new{orderId});
        Require(order is not null,"INVALID_STATE","El pedido no existe.");return await Calculate(w,await DeliveryCart(w,order!,cashBoxId));
    },ct);
    private static async Task<DeliveryData> LockDelivery(Work w,long order,byte[] version)
    {
        await w.Execute("SELECT IdPedido FROM ven.PedidosMvp WITH(UPDLOCK,HOLDLOCK) WHERE IdPedido=@order;",new{order});
        var d=await w.Maybe<DeliveryData>($"SELECT {DeliveryColumns} FROM ven.RepartosMvp WITH(UPDLOCK,HOLDLOCK) WHERE IdPedido=@order;",new{order});
        Require(d is not null,"INVALID_STATE","El pedido no tiene reparto.");Version(d!.RowVersion,version);return d;
    }
    public Task<Result<MvpReceipt>> DispatchDeliveryAsync(DispatchDelivery request,int userId,int? station,CancellationToken ct)
    {
        var r=DeliveryValidator.Freeze(request);
        return Write(r.OperationId,"DESPACHAR_REPARTO",r,userId,station,["VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR"],async w=>
        {
            if(DeliveryValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
            await Station(w,station);var order=await LockOrder(w,r.OrderId,r.ExpectedVersion);
            var q=await Calculate(w,await DeliveryCart(w,order,r.CashBoxId));QuoteToken(q,r.QuoteToken);
            await DiscountPermission(w,q,userId,r.Reason);
            Require(q.Lines.Max(l=>l.EffectiveDiscountPercent)<=q.AuthorizationThreshold,"FORBIDDEN","Este cálculo requiere autorización especial: usa el recorrido de venta previa al reparto.");
            var warehouse=await BoxWarehouse(w,r.CashBoxId);
            var customer=await w.Scalar<string>("SELECT NombreRazonSocial Name,NombreComercial CommercialName,NIT,NRC,DUI,Giro,Telefono Phone,Email FROM cli.Clientes WHERE IdCliente=@CustomerId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;",order);
            var id=await w.Scalar<long>("INSERT INTO ven.RepartosMvp(IdPedido,IdCaja,IdAlmacen,Responsable,Estado,Total,SnapshotCalculo,ClienteSnapshot,IdUsuarioSalida,IdEquipoSalida) OUTPUT INSERTED.IdReparto VALUES(@OrderId,@CashBoxId,@warehouse,@Courier,N'EN_RUTA',@Total,@snapshot,@customer,@userId,@station);",new{r.OrderId,r.CashBoxId,warehouse,r.Courier,q.Total,snapshot=Json(q),customer,userId,station});
            foreach(var line in q.Lines.OrderBy(l=>l.ProductId))
            {
                var stock=await w.One<DeliveryStock>("SELECT StockActual Quantity,StockReservado Reserved,CostoPromedio Cost FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@warehouse AND IdProducto=@ProductId;",new{warehouse,line.ProductId});
                Require(stock.Quantity-stock.Reserved>=line.Quantity,"INSUFFICIENT_STOCK","Cambió la disponibilidad del despacho.");
                var lot=line.Lots.Single().LotId;
                if(lot is not null){var changed=await w.Execute("UPDATE inv.ExistenciasLote SET StockActual=StockActual-@Quantity WHERE IdAlmacen=@warehouse AND IdLote=@lot AND StockActual>=@Quantity;",new{warehouse,lot,line.Quantity});Require(changed==1,"INSUFFICIENT_STOCK","Cambió el lote del despacho.");}
                var after=stock.Quantity-line.Quantity;
                await w.Execute("UPDATE inv.ExistenciasProducto SET StockActual=@after,FechaUltimoMovimiento=SYSDATETIMEOFFSET() WHERE IdAlmacen=@warehouse AND IdProducto=@ProductId;",new{after,warehouse,line.ProductId});
                await w.Execute("INSERT INTO ven.RepartoLineasMvp(IdReparto,IdProducto,IdLote,Cantidad,CostoUnitario,Calculo) VALUES(@id,@ProductId,@lot,@Quantity,@Cost,@calculation);",new{id,line.ProductId,lot,line.Quantity,stock.Cost,calculation=Json(line)});
                await DeliveryMovement(w,warehouse,line.ProductId,lot,-line.Quantity,stock.Cost,stock.Quantity,after,id,userId,"Salida a custodia del repartidor; aún no es venta.");
            }
            await w.Execute("UPDATE ven.PedidosMvp SET Estado=N'EN_RUTA',Responsable=@Courier WHERE IdPedido=@OrderId;",r);
            await OrderEvent(w,r.OrderId,userId,"EN_RUTA",r.Reason);return id;
        },ct);
    }
    private sealed class DeliveryStock { public decimal Quantity{get;set;} public decimal Reserved{get;set;} public decimal Cost{get;set;} }
    private sealed class DeliveryLine { public int ProductId{get;set;} public long? LotId{get;set;} public decimal Quantity{get;set;} public decimal Cost{get;set;} public string Calculation{get;set;}=""; }
    private static Task DeliveryMovement(Work w,int warehouse,int product,long? lot,decimal quantity,decimal cost,decimal before,decimal after,long id,int userId,string reason)=>w.Execute("INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,ReferenciaTipo,ReferenciaId,IdUsuario,Observacion) VALUES(@warehouse,@product,@lot,N'TRANSFERENCIA',@quantity,@cost,@before,@after,N'REPARTO',@id,@userId,@reason);",new{warehouse,product,lot,quantity,cost,before,after,id,userId,reason});
    public Task<Result<MvpReceipt>> CompleteDeliveryAsync(CompleteDelivery request,int userId,int? station,CancellationToken ct)
    {
        var r=DeliveryValidator.Freeze(request);
        return Write(r.OperationId,"RESOLVER_REPARTO",r,userId,station,["VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR"],async w=>
        {
            if(DeliveryValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
            await Station(w,station);var d=await LockDelivery(w,r.OrderId,r.ExpectedVersion);
            Require(d.State is "EN_RUTA" or "INCIDENCIA","INVALID_STATE","El reparto ya fue entregado o retornado.");
            if(r.Outcome=="INCIDENCIA")
            {
                await w.Execute("UPDATE ven.RepartosMvp SET Estado=N'INCIDENCIA' WHERE IdReparto=@Id;",d);
                await w.Execute("UPDATE ven.PedidosMvp SET Estado=N'INCIDENCIA' WHERE IdPedido=@OrderId;",r);
                await OrderEvent(w,r.OrderId,userId,"INCIDENCIA",r.Reason);return d.Id;
            }
            var lines=await w.List<DeliveryLine>("SELECT IdProducto ProductId,IdLote LotId,Cantidad Quantity,CostoUnitario Cost,Calculo Calculation FROM ven.RepartoLineasMvp WHERE IdReparto=@Id ORDER BY IdProducto,IdLinea;",d);
            if(r.Outcome=="RETORNADO")
            {
                foreach(var l in lines)
                {
                    await w.Execute("SELECT IdProducto FROM inv.Productos WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId;",l);
                    var stock=await w.One<DeliveryStock>("SELECT StockActual Quantity,CostoPromedio Cost FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;",new{d.WarehouseId,l.ProductId});
                    var after=stock.Quantity+l.Quantity;var average=decimal.Round((stock.Quantity*stock.Cost+l.Quantity*l.Cost)/after,6,MidpointRounding.AwayFromZero);
                    await w.Execute("UPDATE inv.ExistenciasProducto SET StockActual=@after,CostoPromedio=@average,FechaUltimoMovimiento=SYSDATETIMEOFFSET() WHERE IdAlmacen=@WarehouseId AND IdProducto=@ProductId;",new{after,average,d.WarehouseId,l.ProductId});
                    if(l.LotId is not null)await w.Execute("UPDATE inv.ExistenciasLote SET StockActual=StockActual+@Quantity WHERE IdAlmacen=@WarehouseId AND IdLote=@LotId;",new{d.WarehouseId,l.LotId,l.Quantity});
                    await DeliveryMovement(w,d.WarehouseId,l.ProductId,l.LotId,l.Quantity,l.Cost,stock.Quantity,after,d.Id,userId,"Retorno completo de custodia; mercadería recuperada sellada.");
                }
                await w.Execute("UPDATE ven.RepartosMvp SET Estado=N'RETORNADO',FechaRetorno=SYSDATETIMEOFFSET() WHERE IdReparto=@Id;",d);
                await w.Execute("UPDATE ven.PedidosMvp SET Estado=N'CANCELADO' WHERE IdPedido=@OrderId;",r);
                await OrderEvent(w,r.OrderId,userId,"CANCELADO",r.Reason);return d.Id;
            }
            foreach(var line in lines)
                Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN p.Activo=1 AND p.ControlaSerie=0 AND (p.ControlaLote=0 AND p.ControlaVencimiento=0 OR l.Activo=1 AND (l.FechaVencimiento IS NULL AND p.ControlaVencimiento=0 OR l.FechaVencimiento>=CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time'))) THEN 1 ELSE 0 END) FROM inv.Productos p WITH(UPDLOCK,HOLDLOCK) LEFT JOIN inv.LotesProducto l WITH(HOLDLOCK) ON l.IdLote=@LotId AND l.IdProducto=p.IdProducto WHERE p.IdProducto=@ProductId;",line),"INVALID_STATE","Un producto/lote fue bloqueado o venció durante el reparto. Registra incidencia y recupera la mercadería.");
            Require(r.Amount==d.Total,"VALIDATION","El cobro debe cubrir exactamente el importe congelado del reparto.");
            var code=r.Medium=="EFECTIVO"?"EFECTIVO_REPARTO":"TRANSFERENCIA";
            var method=await w.Scalar<short?>("SELECT IdMetodoPago FROM cat.MetodosPago WITH(HOLDLOCK) WHERE Codigo=@code AND Activo=1 AND AfectaCajaEfectivo=0;",new{code});
            Require(method is not null,"INVALID_STATE","El método de pago del reparto no está disponible.");
            var q=JsonSerializer.Deserialize<SaleQuote>(d.Snapshot)!;var number=await Number(w,"VENTA");
            var customerId=await w.Scalar<long>("SELECT IdCliente FROM ven.PedidosMvp WHERE IdPedido=@OrderId;",r);
            var sale=await w.Scalar<long>("""
                INSERT INTO ven.Ventas(NumeroVenta,IdCliente,IdCaja,IdVendedor,IdEquipo,Estado,Subtotal,DescuentoTotal,ImpuestoTotal,Total,CostoTotal,SaldoPendiente,CargoEntrega,ClienteSnapshotMvp,Observaciones)
                OUTPUT INSERTED.IdVenta VALUES(@number,@customerId,@CashBoxId,@userId,@station,N'CONFIRMADA',@Subtotal,@Discount,@Tax,@Total,@cost,0,@DeliveryCharge,@CustomerSnapshot,@Reason);
                """,new{number,customerId,d.CashBoxId,userId,station,q.Subtotal,q.Discount,q.Tax,q.Total,cost=lines.Sum(l=>l.Quantity*l.Cost),q.Cart.DeliveryCharge,d.CustomerSnapshot,r.Reason});
            foreach(var l in lines)
            {
                var p=JsonSerializer.Deserialize<CalculatedPrice>(l.Calculation)!;
                await w.Execute("INSERT INTO ven.VentaDetalle(IdVenta,IdProducto,IdLote,SkuSnapshot,DescripcionSnapshot,Cantidad,PrecioLista,PrecioUnitario,DescuentoPorcentaje,DescuentoMonto,CostoUnitario,ImpuestoMonto,TotalLinea,CalculoPrecioMvp) VALUES(@sale,@ProductId,@LotId,@Sku,@Name,@Quantity,@PublicPrice,@NetUnitPrice,@percent,@Discount,@Cost,@Tax,@Total,@Calculation);",new{sale,l.ProductId,l.LotId,p.Sku,p.Name,l.Quantity,p.PublicPrice,p.NetUnitPrice,percent=decimal.Ceiling(p.EffectiveDiscountPercent*10000)/10000,p.Discount,l.Cost,p.Tax,p.Total,l.Calculation});
            }
            await w.Execute("INSERT INTO ven.VentaPagos(IdVenta,IdMetodoPago,Monto,Referencia,IdUsuario,Estado) VALUES(@sale,@method,@Total,@Reference,@userId,N'CONFIRMADO');",new{sale,method,d.Total,r.Reference,userId});
            await w.Execute("UPDATE ven.RepartosMvp SET Estado=N'PENDIENTE_RENDICION',IdVenta=@sale,Medio=@Medium,Referencia=@Reference,FechaEntrega=SYSDATETIMEOFFSET() WHERE IdReparto=@Id;",new{sale,r.Medium,r.Reference,d.Id});
            await w.Execute("UPDATE ven.PedidosMvp SET Estado=N'ENTREGADO',IdVenta=@sale,FechaEntrega=SYSDATETIMEOFFSET(),EntregadoPor=@userId WHERE IdPedido=@OrderId;",new{sale,userId,r.OrderId});
            await OrderEvent(w,r.OrderId,userId,"ENTREGADO",r.Reason);return d.Id;
        },ct);
    }
    public Task<Result<MvpReceipt>> SettleDeliveryAsync(SettleDelivery request,int userId,int? station,CancellationToken ct)
    {
        var r=DeliveryValidator.Freeze(request);
        return Write(r.OperationId,"RENDIR_REPARTO",r,userId,station,["VENTAS.VER","CAJA.VER","CAJA.REPARTO_RENDIR"],async w=>
        {
            if(DeliveryValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
            await Station(w,station);var d=await LockDelivery(w,r.OrderId,r.ExpectedVersion);
            Require(d.State=="PENDIENTE_RENDICION"&&r.Amount==d.Total,"INVALID_STATE","La rendición debe cubrir exactamente un reparto pendiente.");
            if(d.Medium=="EFECTIVO")
            {
                Require(r.ShiftId is not null,"VALIDATION","Selecciona un turno abierto para recibir el efectivo.");
                await LockShift(w,r.ShiftId!.Value,d.CashBoxId);
                await w.Execute("INSERT INTO ven.MovimientosCaja(IdTurno,Tipo,Monto,ReferenciaTipo,ReferenciaId,IdUsuario,Motivo) VALUES(@ShiftId,N'VENTA_EFECTIVO',@Total,N'VENTA',@SaleId,@userId,@Reason);",new{r.ShiftId,d.Total,d.SaleId,userId,r.Reason});
                await TouchShift(w,r.ShiftId.Value);
            }
            else Require(r.ShiftId is null&&!string.IsNullOrWhiteSpace(r.Reference)&&r.Reference==d.Reference,"VALIDATION","Verifica y repite exactamente la referencia de transferencia; no corresponde turno de efectivo.");
            await w.Execute("UPDATE ven.RepartosMvp SET Estado=N'RENDIDO',FechaRendicion=SYSDATETIMEOFFSET(),IdUsuarioRendicion=@userId,IdEquipoRendicion=@station,IdTurno=@ShiftId WHERE IdReparto=@Id;",new{userId,station,r.ShiftId,d.Id});
            await w.Execute("UPDATE ven.PedidosMvp SET Responsable=Responsable WHERE IdPedido=@OrderId;",r);
            await OrderEvent(w,r.OrderId,userId,"ENTREGADO",r.Reason);return d.Id;
        },ct);
    }
}
