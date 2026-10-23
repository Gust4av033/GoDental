using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
internal static class DeliveryReportIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("OK "+name);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException(r.ErrorCode+": "+r.Message);
        await using var c=await factory.OpenConnectionAsync();var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());
        var identity=(await gateway.CreateAdministratorAsync("delivery_test","Administrador reparto",Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),"DELIVERY-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var repo=new SqlMvpRepository(factory);var delivery=new DeliveryService(repo,session);var sales=new SalesService(repo,session,gateway);var cash=new CashService(repo,session);var orders=new OrderService(repo,session);
        var reports=new ReportService(repo,new OperationalReportRenderer(Options.Create(new ReceiptPdfOptions())),session);
        var products=new ProductService(new SqlProductRepository(factory),session);var inventory=new InventorySetupService(repo,session);var catalog=(await products.GetCatalogsAsync()).Value!;
        var warehouse=await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        async Task<int> Product(string sku,bool lot=false)
        {
            var created=await products.SaveAsync(new(){Sku=sku,Name="Producto "+sku,CategoryId=catalog.Categories[0].Id,UnitId=(short)catalog.Units.First(u=>u.Name.Contains("Unidad")).Id,Price=10,TracksLot=lot,TracksExpiry=lot});
            if(!created.IsSuccess)throw new InvalidOperationException(created.Message);var id=(int)created.Value;
            Saved(await inventory.InitializeAsync(new(Guid.NewGuid(),id,warehouse,20,4,"Saldo aislado",lot?"COD-LOTE":null,Expires:lot?DateTime.Today.AddDays(15):null)));return id;
        }
        var product=await Product("COD-A");var lotProduct=await Product("COD-L",true);
        var customers=new CustomerService(new SqlCustomerRepository(factory),session);var type=(short)(await customers.CatalogsAsync()).Types[0].Id;
        var customer=(await customers.SaveAsync(new(new(){Name="Cliente sin crédito de reparto",TypeId=type},"Alta aislada"))).Value;
        var box=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"COD-CAJA","Caja reparto",warehouse,true,"Alta aislada")));
        var shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,100,"Apertura aislada")));
        var today=(await c.ExecuteScalarAsync<DateTimeOffset>("SELECT SYSDATETIMEOFFSET();")).ToOffset(TimeSpan.FromHours(-6)).Date;
        ReportFilter Filter(OperationalReport kind)=>new(kind,today.AddDays(-1),today.AddDays(1));
        async Task<OrderSnapshot> Order(decimal quantity=2,int? p=null)
        {
            var id=Saved(await orders.PlaceAsync(new(Guid.NewGuid(),customer,"Contacto","70000000","Dirección aislada",null,null,null,null,"Repartidor",null,3,[new(p??product,quantity)],"Pedido aislado")));
            foreach(var state in new[]{"PREPARANDO","LISTO"}){var o=(await orders.ReadAsync(id))!.Order;Saved(await orders.TransitionAsync(new(Guid.NewGuid(),id,o.RowVersion,state,"Preparación aislada")));}
            return(await orders.ReadAsync(id))!;
        }
        async Task<DispatchDelivery> Dispatch(OrderSnapshot o)
        {var q=await delivery.QuoteAsync(o.Order.Id,box);return new(Guid.NewGuid(),o.Order.Id,o.Order.RowVersion,box,q.Token,"Repartidor de prueba","Salida de prueba");}
        Task<decimal> Stock(int p)=>c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse});
        var order=await Order();var dispatch=await Dispatch(order);var before=await Stock(product);var rid=Saved(await delivery.DispatchAsync(dispatch));
        Check(Saved(await delivery.DispatchAsync(dispatch))==rid,"Despacho recuperable genera una sola custodia");
        Check(await Stock(product)==before-2,"Despacho retira unidades físicas de bodega");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;")==0&&await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.CuentasPorCobrar;")==0,"Custodia no crea venta ni deuda comercial");
        Check((await cash.ReadAsync(shift))!.Expected==100,"Despachar no ingresa efectivo");
        var custody=await reports.ReadAsync(Filter(OperationalReport.Inventory));var ir=custody.Rows.Single(x=>x.Document=="COD-A"&&x.Stock>0);
        Check(ir.Stock==18&&ir.InRoute==2&&ir.Value+ir.RouteValue==80,"Reporte incluye bodega y custodia sin perder valor empresarial");
        Check(!(await delivery.DispatchAsync(dispatch with{OperationId=Guid.NewGuid()})).IsSuccess,"Otra estación no vuelve a despachar el pedido");
        var changed=(await orders.ReadAsync(order.Order.Id))!.Order;
        Check(!(await orders.TransitionAsync(new(Guid.NewGuid(),changed.Id,changed.RowVersion,"ENTREGADO","Intento genérico"))).IsSuccess,"Transición genérica no evita el cobro del reparto");
        var data=(await delivery.ReadAsync(order.Order.Id))!;
        Check(!(await delivery.CompleteAsync(new(Guid.NewGuid(),data.OrderId,data.RowVersion,"ENTREGADO","EFECTIVO",null,data.Total-1,false,"Monto incorrecto"))).IsSuccess,"No confirma entrega con cobro parcial");
        var completion=new CompleteDelivery(Guid.NewGuid(),data.OrderId,data.RowVersion,"ENTREGADO","EFECTIVO",null,data.Total,false,"Entregado y cobrado completo");
        Saved(await delivery.CompleteAsync(completion));Saved(await delivery.CompleteAsync(completion));data=(await delivery.ReadAsync(data.OrderId))!;
        Check(data.State=="PENDIENTE_RENDICION"&&data.SaleId is not null,"Entrega emite venta y queda pendiente de rendición");
        var sale=(await sales.ReadAsync(data.SaleId!.Value))!;
        Check(sale.Sale.Pending==0&&sale.Sale.Total==23&&sale.Payments.Single().Method.Contains("reparto"),"Venta pagada distingue efectivo en reparto sin crédito");
        Check(await Stock(product)==before-2&&await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.Ventas;")==1,"Confirmar entrega no descuenta stock ni vende dos veces");
        Check((await cash.ReadAsync(shift))!.Expected==100,"Cobro con repartidor no se cuenta en caja");
        Check(!(await sales.PaymentMethodsAsync()).Any(x=>x.Code=="EFECTIVO_REPARTO"),"POS no ofrece método exclusivo del reparto");
        var method=await c.ExecuteScalarAsync<short>("SELECT IdMetodoPago FROM cat.MetodosPago WHERE Codigo=N'EFECTIVO_REPARTO';");
        var cart=new SaleCart(box,customer,[new(product,1)]);var quote=await sales.QuoteAsync(cart);
        Check(!(await sales.ConfirmAsync(new(Guid.NewGuid(),cart,quote.Token,[new(method,quote.Total)],0,null,"Intento fuera de reparto"))).IsSuccess,"SQL rechaza método exclusivo desde venta normal");
        var returns=new SaleReturnService(repo,session,gateway);var req=new RequestSaleReturn(Guid.NewGuid(),sale.Sale.Id,sale.Sale.RowVersion,false,sale.Sale.Number,"Devolución de prueba",[new(sale.Lines[0].Id,1,"SELLADO",true,null,[])]);
        var requestId=Saved(await returns.RequestAsync(req));Check((await returns.DetailAsync(sale.Sale.Id,requestId))!.UnavailableReason is not null,"Posventa espera rendición antes de reintegrar");
        Check(!(await delivery.SettleAsync(new(Guid.NewGuid(),data.OrderId,data.RowVersion,shift,data.Total-1,null,"Monto menor"))).IsSuccess,"Rendición no acepta diferencias sin resolver");
        var close=(await cash.ReadAsync(shift))!;Saved(await cash.CloseAsync(new(Guid.NewGuid(),shift,close.RowVersion,100,"Cierre antes del regreso")));
        Check(!(await delivery.SettleAsync(new(Guid.NewGuid(),data.OrderId,data.RowVersion,shift,data.Total,null,"Turno cerrado"))).IsSuccess,"Efectivo no ingresa a turno cerrado");
        shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,50,"Nuevo turno")));var oldShift=(await cash.ReadAsync(shift))!;
        var settle=new SettleDelivery(Guid.NewGuid(),data.OrderId,data.RowVersion,shift,data.Total,null,"Efectivo recibido físicamente");var race=await Task.WhenAll(delivery.SettleAsync(settle),delivery.SettleAsync(settle with{OperationId=Guid.NewGuid()}));
        Check(race.Count(x=>x.IsSuccess)==1,"Dos estaciones no reciben dos veces el mismo efectivo");
        Check((await cash.ReadAsync(shift))!.Expected==73,"Sólo rendir agrega el efectivo al turno actual");
        Check(!(await cash.CloseAsync(new(Guid.NewGuid(),shift,oldShift.RowVersion,73,"Versión anterior"))).IsSuccess,"Rendición invalida arqueo calculado antes de recibir dinero");
        Check((await returns.DetailAsync(sale.Sale.Id,requestId))!.UnavailableReason is null,"Después de rendir se habilita posventa ordinaria");
        var pendingReturn=(await returns.DetailAsync(sale.Sale.Id,requestId))!;
        Saved(await repo.ResolveReturnAsync(new(Guid.NewGuid(),sale.Sale.Id,requestId,pendingReturn.Return.RowVersion,true,pendingReturn.QuoteToken,shift,"Aplicar devolución sellada"),identity.Id,identity.Id,identity.WorkstationId,default));
        var report=await reports.ReadAsync(Filter(OperationalReport.Sales));Check(report.Total==13&&report.Net==10&&report.Cost==4&&report.Margin==6,"Ventas menos devolución concilian ingreso, entrega y costo histórico");
        Check(report.Rows.Any(x=>x.Note=="Posventa aplicada"&&x.Amount==-10),"Reporte conserva compensación de devolución separada");
        foreach(var group in Enum.GetValues<ReportGrouping>()){var g=ReportService.Group(report with{Filter=report.Filter with{Group=group}});Check(g.Sum(x=>x.Amount)==report.Total,"Agrupación concilia: "+group);}
        var transferOrder=await Order(1,lotProduct);var ld=await Dispatch(transferOrder);Saved(await delivery.DispatchAsync(ld));var lotData=(await delivery.ReadAsync(ld.OrderId))!;
        var lotBefore=await Stock(lotProduct);var transfer=new CompleteDelivery(Guid.NewGuid(),ld.OrderId,lotData.RowVersion,"ENTREGADO","TRANSFERENCIA","BANCO-PRUEBA",lotData.Total,false,"Transferencia confirmada por cliente");Saved(await delivery.CompleteAsync(transfer));lotData=(await delivery.ReadAsync(ld.OrderId))!;
        Check(await Stock(lotProduct)==lotBefore,"Entrega por transferencia conserva único descuento del lote");
        var cashBefore=(await cash.ReadAsync(shift))!.Expected;
        Check(!(await delivery.SettleAsync(new(Guid.NewGuid(),ld.OrderId,lotData.RowVersion,null,lotData.Total,"OTRA","Referencia distinta"))).IsSuccess,"Transferencia exige verificar referencia exacta");
        var tr=new SettleDelivery(Guid.NewGuid(),ld.OrderId,lotData.RowVersion,null,lotData.Total,"BANCO-PRUEBA","Referencia verificada en banco");Saved(await delivery.SettleAsync(tr));Saved(await delivery.SettleAsync(tr));
        Check((await cash.ReadAsync(shift))!.Expected==cashBefore,"Rendir transferencia no aumenta efectivo");
        var failedOrder=await Order(2,lotProduct);var fd=await Dispatch(failedOrder);var originalLot=await Stock(lotProduct);Saved(await delivery.DispatchAsync(fd));var failed=(await delivery.ReadAsync(fd.OrderId))!;
        await c.ExecuteAsync("UPDATE inv.LotesProducto SET Activo=0 WHERE IdProducto=@lotProduct;",new{lotProduct});
        Check(!(await delivery.CompleteAsync(new(Guid.NewGuid(),fd.OrderId,failed.RowVersion,"ENTREGADO","EFECTIVO",null,failed.Total,false,"Lote bloqueado en ruta"))).IsSuccess,"Lote bloqueado durante reparto no se entrega");
        await c.ExecuteAsync("UPDATE inv.LotesProducto SET Activo=1 WHERE IdProducto=@lotProduct;",new{lotProduct});
        Saved(await delivery.CompleteAsync(new(Guid.NewGuid(),fd.OrderId,failed.RowVersion,"INCIDENCIA",null,null,0,false,"Cliente ausente; mercadería con repartidor")));failed=(await delivery.ReadAsync(fd.OrderId))!;
        Check(failed.State=="INCIDENCIA"&&await Stock(lotProduct)==originalLot-2,"Entrega fallida conserva custodia hasta recuperación");
        Check(!(await delivery.CompleteAsync(new(Guid.NewGuid(),fd.OrderId,failed.RowVersion,"RETORNADO",null,null,0,false,"Sin recuperar todo"))).IsSuccess,"No fabrica stock con retorno incompleto");
        var ret=new CompleteDelivery(Guid.NewGuid(),fd.OrderId,failed.RowVersion,"RETORNADO",null,null,0,true,"Todo sellado, recuperado y revisado");Saved(await delivery.CompleteAsync(ret));Saved(await delivery.CompleteAsync(ret));
        Check(await Stock(lotProduct)==originalLot&&(await delivery.ReadAsync(fd.OrderId))!.State=="RETORNADO","Retorno completo repone exactamente un lote sin duplicación");
        Check((await orders.ReadAsync(fd.OrderId))!.Order.State=="CANCELADO"&&(await orders.ReadAsync(fd.OrderId))!.Order.SaleId is null,"Retorno fallido no vende ni cobra entrega");
        var originBox=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"COD-SIN-TURNO","Caja sin turno",warehouse,true,"Alta aislada")));
        var originOrder=await Order(1);var originQuote=await delivery.QuoteAsync(originOrder.Order.Id,originBox);
        Saved(await delivery.DispatchAsync(new(Guid.NewGuid(),originOrder.Order.Id,originOrder.Order.RowVersion,originBox,originQuote.Token,"Repartidor","Salida sin turno")));
        Check(!(await cash.SaveBoxAsync(new(Guid.NewGuid(),originBox,"COD-CAMBIO","Caja sin turno",warehouse,true,"Cambiar caja con custodia"))).IsSuccess,"Caja con custodia conserva código y almacén aunque aún no tenga turno ni venta");
        var originDelivery=(await delivery.ReadAsync(originOrder.Order.Id))!;
        Saved(await delivery.CompleteAsync(new(Guid.NewGuid(),originDelivery.OrderId,originDelivery.RowVersion,"RETORNADO",null,null,0,true,"Recuperación de prueba")));
        var midnight=new DateTimeOffset(DateTime.SpecifyKind(today.AddDays(1),DateTimeKind.Unspecified),TimeSpan.FromHours(-6)).ToUniversalTime();
        await c.ExecuteAsync("UPDATE ven.Ventas SET Fecha=@date WHERE IdVenta=@sale;",new{date=midnight.AddSeconds(-1),sale=lotData.SaleId});
        var boundaryNumber=(await sales.ReadAsync(lotData.SaleId!.Value))!.Sale.Number;
        Check((await reports.ReadAsync(new(OperationalReport.Sales,today,today))).Rows.Any(x=>x.Document==boundaryNumber),"Último segundo local pertenece al período inclusivo");
        await c.ExecuteAsync("UPDATE ven.Ventas SET Fecha=@midnight WHERE IdVenta=@sale;",new{midnight,sale=lotData.SaleId});
        var transferNumber=(await sales.ReadAsync(lotData.SaleId!.Value))!.Sale.Number;
        Check(!(await reports.ReadAsync(new(OperationalReport.Sales,today,today))).Rows.Any(x=>x.Document==transferNumber),"Medianoche del día siguiente se excluye sin perder zona horaria");
        await c.ExecuteAsync("UPDATE ven.Ventas SET Fecha=@date WHERE IdVenta=@sale;",new{date=lotData.Delivered,sale=lotData.SaleId});
        var badOrder=await Order();var bad=await Dispatch(badOrder);var beforeFail=await Stock(product);
        await c.ExecuteAsync("CREATE TRIGGER sis.TR_TestDeliveryAudit ON sis.Bitacora AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion=N'DESPACHAR_REPARTO') THROW 59998,N'Fallo aislado de auditoría',1; END;");
        var failedWrite=false;try{await delivery.DispatchAsync(bad);}catch(SqlException e)when(e.Number==59998){failedWrite=true;}await c.ExecuteAsync("DROP TRIGGER sis.TR_TestDeliveryAudit;");
        Check(failedWrite&&await Stock(product)==beforeFail&&await delivery.ReadAsync(bad.OrderId)is null,"Fallo de auditoría revierte custodia, stock y estado");
        Check(await repo.FindOperationAsync(bad.OperationId,identity.Id,default)is null,"Rollback no deja recibo de despacho confirmado");
        var absent=OperationIdentity.Describe(bad);Check((await repo.ResolveOperationAsync(absent,true,identity.Id,identity.WorkstationId,default)).State==OperationResolutionState.RejectedDefinitively,"Reparto incierto puede resolverse sin efectos");
        Check((await delivery.DispatchAsync(bad)).ErrorCode=="OPERATION_REJECTED","Solicitud rechazada definitivamente no se confirma después");
        var expiry=await reports.ReadAsync(Filter(OperationalReport.Expiry));Check(expiry.Rows.Any(x=>x.Document=="COD-L"&&x.Quantity>0),"Reporte muestra lotes próximos a vencer y sus unidades");
        var finalStock=await reports.ReadAsync(Filter(OperationalReport.Inventory));Check(finalStock.Rows.All(x=>x.InRoute==0),"Custodia se vacía sólo al entregar o retornar");
        foreach(var kind in Enum.GetValues<OperationalReport>())Check((await reports.PreviewAsync(Filter(kind),PrintLayout.Letter)).Pages.Count>0,"PDF real del reporte "+kind);
        var suppliers=new SupplierService(new SqlSupplierRepository(factory),session);
        var supplier=(await suppliers.SaveAsync(new(new(){Name="Proveedor reportes"},"Alta aislada"))).Value;
        var purchases=new PurchaseService(repo,session);var usd=(await purchases.CatalogsAsync()).Currencies.Single(x=>x.Code=="USD").Id;
        var oc=Saved(await purchases.SaveAsync(new(Guid.NewGuid(),0,[],supplier,usd,1,today,[new(product,2,5)],"Compra de prueba")));
        var po=(await purchases.ReadAsync(oc))!;Saved(await purchases.TransitionAsync(new(Guid.NewGuid(),oc,po.Order.RowVersion,"CONFIRMADA","Confirmar compra")));po=(await purchases.ReadAsync(oc))!;
        var rc=Saved(await purchases.PrepareAsync(new(Guid.NewGuid(),oc,po.Order.RowVersion,warehouse,1,true,[new(po.Lines[0].Id,2,1)],"Preparar recepción")));
        Check((await reports.ReadAsync(Filter(OperationalReport.Purchases))).Rows.Count==0,"Borradores de recepción no se suman a compras recibidas");
        var receipt=(await purchases.ReceiptAsync(rc))!;po=(await purchases.ReadAsync(oc))!;
        Saved(await purchases.ApplyAsync(new(Guid.NewGuid(),rc,receipt.Receipt.RowVersion,po.Order.RowVersion,true,"Aplicar recepción")));
        var purchasesReport=await reports.ReadAsync(Filter(OperationalReport.Purchases));Check(purchasesReport.Total==12&&purchasesReport.Rows.Sum(x=>x.Freight)==2,"Compras concilian costo mercadería y gastos una sola vez");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cmp.RecepcionesCompra WHERE IdRecepcion=@rc AND FechaAplicacionMvp IS NOT NULL;",new{rc})==1,"Nueva recepción conserva fecha real de aplicación");
        await c.ExecuteAsync("UPDATE cmp.RecepcionesCompra SET FechaAplicacionMvp=NULL WHERE IdRecepcion=@rc;",new{rc});
        Check((await reports.ReadAsync(Filter(OperationalReport.Purchases))).Rows.Single().Note.Contains("Histórico"),"Recepción antigua indica fecha histórica sin reconstruir aplicación");
        var centProduct=await Product("COD-CENTAVOS");await c.ExecuteAsync("UPDATE inv.Productos SET PrecioVentaReferencia=0.05 WHERE IdProducto=@centProduct; INSERT INTO inv.ProductoImpuestos(IdProducto,IdImpuesto) SELECT @centProduct,IdImpuesto FROM cat.Impuestos WHERE Codigo=N'IVA';",new{centProduct});
        var centCart=new SaleCart(box,customer,[new(centProduct,3)]);var centQuote=await sales.QuoteAsync(centCart);
        var transferMethod=(await sales.PaymentMethodsAsync()).Single(x=>x.Code=="TRANSFERENCIA").Id;
        var centSale=Saved(await sales.ConfirmAsync(new(Guid.NewGuid(),centCart,centQuote.Token,[new(transferMethod,centQuote.Total,"CENT-PRUEBA")],0,null,"Venta fraccionada para centavos")));
        var centSaleData=(await sales.ReadAsync(centSale))!;
        Check(centSaleData.Sale.Tax>0,"Fixture registra impuesto real para comprobar remanentes");
        for(var n=0;n<3;n++)
        {
            var cs=(await sales.ReadAsync(centSale))!;var cr=Saved(await returns.RequestAsync(new(Guid.NewGuid(),centSale,cs.Sale.RowVersion,false,cs.Sale.Number,"Devolución de centavos",[new(cs.Lines[0].Id,1,"SELLADO",true,null,[])])));
            var detail=(await returns.DetailAsync(centSale,cr))!;
            Saved(await repo.ResolveReturnAsync(new(Guid.NewGuid(),centSale,cr,detail.Return.RowVersion,true,detail.QuoteToken,shift,"Aplicación de centavos"),identity.Id,identity.Id,identity.WorkstationId,default));
        }
        var cents=(await reports.ReadAsync(Filter(OperationalReport.Sales))).Rows.Where(x=>x.ProductId==centProduct).ToArray();
        Check(cents.Sum(x=>x.Amount)==0&&cents.Sum(x=>x.Tax)==0&&cents.Sum(x=>x.Net)==0,"Devoluciones parciales agotan impuesto y base exactos sin perder centavos");
        var versions=(await orders.ReadAsync(fd.OrderId))!.Order.RowVersion;await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));
        Check((await orders.ReadAsync(fd.OrderId))!.Order.RowVersion.SequenceEqual(versions)&&(await delivery.ReadAsync(fd.OrderId))!.State=="RETORNADO","Reaplicar 022/023 conserva historia y versiones");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.COSTOS_VER');");
        var hidden=await reports.ReadAsync(Filter(OperationalReport.Sales));Check(!hidden.CostsVisible&&hidden.Rows.All(x=>x.Cost is null)&&hidden.Margin is null,"SQL oculta costo y margen con permiso revocado aunque sesión lo recuerde");
        var hiddenStock=await reports.ReadAsync(Filter(OperationalReport.Inventory));Check(hiddenStock.Rows.All(x=>x.Cost is null&&x.Value is null&&x.RouteValue is null),"Inventario no filtra costos ocultos en otra columna");
        var denied=false;try{await reports.ReadAsync(Filter(OperationalReport.Purchases));}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Reporte de compras no usa permiso de ventas para revelar costos");
        var forbidden=await Dispatch(await Order());
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'VENTAS.REPARTO_GESTIONAR');");
        Check((await delivery.DispatchAsync(forbidden)).ErrorCode=="FORBIDDEN","SQL revoca permiso de despacho pese a sesión cacheada");
        Console.WriteLine($"{checks} comprobaciones de reparto y reportes SQL correctas.");
    }
}
