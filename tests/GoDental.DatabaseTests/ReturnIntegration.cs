using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Data;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;

internal static class ReturnIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;Console.WriteLine("OK "+text);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException($"{r.ErrorCode}: {r.Message}");
        await using var c=await factory.OpenConnectionAsync();var secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());var identity=(await gateway.CreateAdministratorAsync("returns_test","Administrador de posventa",secret,"RETURNS-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var evidence=new FileReturnEvidenceStore(factory);var repo=new SqlMvpRepository(factory,evidence);var service=new SaleReturnService(repo,session,gateway,evidence);
        var sales=new SalesService(repo,session,gateway);var cash=new CashService(repo,session);var inventory=new InventorySetupService(repo,session);var products=new ProductService(new SqlProductRepository(factory),session);
        var evidenceRoot=Path.Combine(Path.GetTempPath(),"GoDental-Return-"+Guid.NewGuid().ToString("N"));
        var location=await service.EvidenceLocationAsync();var configure=new ConfigureReturnEvidence(Guid.NewGuid(),evidenceRoot,location.RowVersion,"Carpeta aislada de prueba");
        Saved(await service.ConfigureAsync(configure));Check(Saved(await service.ConfigureAsync(configure))==1,"Configuración central es recuperable sin duplicación");
        Check((await service.EvidenceLocationAsync()).Root==evidenceRoot,"Las estaciones leen una misma raíz desde SQL");
        var catalogs=(await products.GetCatalogsAsync()).Value!;var warehouse=await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        async Task<int> Product(string sku,decimal price,bool lot=false,bool decimals=false)
        {
            var id=(await products.SaveAsync(new(){Sku=sku,Name="Producto "+sku,CategoryId=catalogs.Categories[0].Id,UnitId=(short)catalogs.Units.Single(u=>u.Name==(decimals?"Gramo":"Unidad")).Id,Price=price,TracksLot=lot,TracksExpiry=lot})).Value;
            Saved(await inventory.InitializeAsync(new(Guid.NewGuid(),(int)id,warehouse,100,4,"Inicial de prueba",lot?"L-RET":null,Expires:lot?DateTime.Today.AddYears(1):null)));return(int)id;
        }
        var p=await Product("RET-A",10);var q=await Product("RET-LOTE",20,true);var fraction=await Product("RET-FRAC",.05m,decimals:true);
        var box=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"RET-CAJA","Caja posventa",warehouse,true,"Alta aislada")));var shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,500,"Fondo inicial")));
        var methods=await sales.PaymentMethodsAsync();var effective=methods.Single(x=>x.Code=="EFECTIVO").Id;var credit=methods.Single(x=>x.Code=="CREDITO").Id;
        async Task<SaleSnapshot> Sale(int? product=null,decimal quantity=3,long? customer=null,bool onCredit=false,decimal delivery=0)
        {
            var cart=new SaleCart(box,customer,[new(product??p,quantity)],DeliveryCharge:delivery);var quote=await sales.QuoteAsync(cart);
            var id=Saved(await sales.ConfirmAsync(new(Guid.NewGuid(),cart,quote.Token,[new(onCredit?credit:effective,quote.Total)],onCredit?0:quote.Total,onCredit?null:shift,"Venta de prueba")));return(await sales.ReadAsync(id))!;
        }
        async Task<(RequestSaleReturn Request,long Id)> Request(long saleId,decimal quantity=1,bool restock=true,bool cancel=false,ReturnEvidence? photo=null,string condition="SELLADO")
        {
            var sale=(await sales.ReadAsync(saleId))!;var request=new RequestSaleReturn(Guid.NewGuid(),saleId,sale.Sale.RowVersion,cancel,sale.Sale.Number,"Motivo de prueba",[new(sale.Lines[0].Id,quantity,condition,restock,restock?null:"Identificado como daño; no disponible",photo is null?[]:[photo])]);return(request,Saved(await service.RequestAsync(request)));
        }
        async Task<ResolveSaleReturn> Resolution(long saleId,long id,bool apply=true)
        {var d=(await service.DetailAsync(saleId,id))!;return new(Guid.NewGuid(),saleId,id,d.Return.RowVersion,apply,d.QuoteToken,apply&&d.CashRefund>0?shift:null,"Resolución de prueba");}
        Task<Result<MvpReceipt>> Resolve(ResolveSaleReturn r)=>repo.ResolveReturnAsync(r,identity.Id,identity.Id,identity.WorkstationId,default);
        var first=await Sale();var stockBefore=await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse});var cashBefore=(await cash.ReadAsync(shift))!.Expected;
        var requested=await Request(first.Sale.Id);Check(Saved(await service.RequestAsync(requested.Request))==requested.Id,"Reenvío de solicitud conserva el número y detalle");
        Check((await cash.ReadAsync(shift))!.Expected==cashBefore&&await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse})==stockBefore,"Solicitar no mueve caja ni stock");
        await c.ExecuteAsync("UPDATE inv.ExistenciasProducto SET CostoPromedio=8 WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse});
        var resolve=await Resolution(first.Sale.Id,requested.Id);Check(!(await service.ResolveAsync(resolve,identity.UserName,"incorrecta")).IsSuccess,"Credenciales incorrectas no autorizan el reintegro");
        Saved(await service.ResolveAsync(resolve,identity.UserName,secret));Check(Saved(await Resolve(resolve))==requested.Id,"Reenvío de aplicación no duplica reintegro");
        var applied=(await service.DetailAsync(first.Sale.Id,requested.Id))!;Check(applied.Return.State=="APLICADA"&&applied.CashRefund==10&&applied.DebtReduction==0,"Contado compensa en efectivo");
        Check((await cash.ReadAsync(shift))!.Expected==cashBefore-10,"Caja actual descuenta un solo reintegro");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse})==stockBefore+1,"Vendible reingresa una sola unidad");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT CostoPromedio FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse;",new{p,warehouse})==7.959184m,"Reingreso recalcula promedio actual con costo histórico, sin revaluar salidas anteriores");
        var original=(await sales.ReadAsync(first.Sale.Id))!;Check(original.Sale.State=="DEVUELTA_PARCIAL"&&original.Sale.Total==30&&original.Payments.Single().Amount==30&&original.Lines[0].Quantity==3,"Conserva importe, pago y cantidad originales");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT CostoUnitario FROM inv.MovimientosInventario WHERE ReferenciaTipo=N'DEVOLUCION' AND ReferenciaId=@id;",new{id=requested.Id})==4,"Kardex usa costo histórico de venta");
        Check((await repo.ResolveOperationAsync(OperationIdentity.Describe(resolve),false,identity.Id,identity.WorkstationId,default)).State==OperationResolutionState.Confirmed,"Recuperación encuentra aplicación tras respuesta perdida");
        var excessive=requested.Request with{OperationId=Guid.NewGuid(),ExpectedSaleVersion=original.Sale.RowVersion,Items=[requested.Request.Items[0] with{Quantity=3}]};Check(!(await service.RequestAsync(excessive)).IsSuccess,"No solicita cantidades superiores a lo pendiente");
        Check(!(await service.RequestAsync(excessive with{Items=[excessive.Items[0] with{SaleLineId=long.MaxValue,Quantity=1}]})).IsSuccess,"Rechaza detalle que no pertenece a la venta");
        var stale=await Request(first.Sale.Id);var reject=await Resolution(first.Sale.Id,stale.Id,false);Saved(await Resolve(reject));Check((await service.DetailAsync(first.Sale.Id,stale.Id))!.Return.State=="RECHAZADA"&&(await cash.ReadAsync(shift))!.Expected==cashBefore-10,"Rechazar conserva solicitud sin efectos");
        var raceSale=await Sale(quantity:1);var a=await Request(raceSale.Sale.Id);var b=await Request(raceSale.Sale.Id);var ra=await Resolution(raceSale.Sale.Id,a.Id);var rb=await Resolution(raceSale.Sale.Id,b.Id);
        var races=await Task.WhenAll(Resolve(ra),Resolve(rb));Check(races.Count(x=>x.IsSuccess)==1,"Dos equipos no devuelven dos veces la misma unidad");
        Check((await service.ReadAsync(raceSale.Sale.Id)).Lines.Single().Returned==1,"Acumulado nunca excede la cantidad vendida");
        var loser=races[0].IsSuccess?b:a;var loserSnapshot=(await service.DetailAsync(raceSale.Sale.Id,loser.Id))!;Check(loserSnapshot.UnavailableReason is not null,"Solicitud competidora explica por qué ya no puede aplicarse");
        var lotSale=await Sale(q,2);var photo=await service.UploadPhotoAsync(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRusAAAAASUVORK5CYII="),"defecto.png");var damaged=await Request(lotSale.Sale.Id,restock:false,photo:photo,condition:"ABIERTO_DEFECTUOSO");var lotStock=await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasLote WHERE IdLote=@id AND IdAlmacen=@warehouse;",new{id=lotSale.Lines[0].LotId,warehouse});
        var photoBytes=await evidence.ReadAsync(photo);var photoFile=Path.Combine(evidenceRoot,photo.RelativePath.Replace('/',Path.DirectorySeparatorChar));var changed=photoBytes.ToArray();changed[35]^=1;await File.WriteAllBytesAsync(photoFile,changed);
        var damagedAction=await Resolution(lotSale.Sale.Id,damaged.Id);Check(!(await Resolve(damagedAction)).IsSuccess,"Foto alterada bloquea efectos aunque ya existiera la solicitud");await File.WriteAllBytesAsync(photoFile,photoBytes);
        Saved(await Resolve(damagedAction));Check(await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasLote WHERE IdLote=@id AND IdAlmacen=@warehouse;",new{id=lotSale.Lines[0].LotId,warehouse})==lotStock,"Defectuoso se identifica sin aumentar stock vendible del lote");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ven.DevolucionEvidencias e JOIN ven.DevolucionDetalle d ON d.IdDetalle=e.IdDevolucionDetalle WHERE d.IdDevolucion=@id;",new{id=damaged.Id})==1,"Foto queda vinculada al detalle original de devolución");
        location=await service.EvidenceLocationAsync();Check(!(await repo.ConfigureEvidenceAsync(new(Guid.NewGuid(),evidenceRoot+"-moved",location.RowVersion,"Traslado sin copia"),identity.Id,identity.WorkstationId,default)).IsSuccess,"No cambia raíz ni pierde vínculos después de registrar fotos");
        var noPhoto=(await sales.ReadAsync(lotSale.Sale.Id))!;Check(!(await service.RequestAsync(new(Guid.NewGuid(),noPhoto.Sale.Id,noPhoto.Sale.RowVersion,false,"Constancia","Defecto",[new(noPhoto.Lines[0].Id,1,"ABIERTO_DEFECTUOSO",false,"Falla",[]) ]))).IsSuccess,"Abierto sin foto no se solicita");
        var expired=await Request(lotSale.Sale.Id);await c.ExecuteAsync("UPDATE inv.LotesProducto SET Activo=0 WHERE IdLote=@id;",new{id=lotSale.Lines[0].LotId});Check(!(await Resolve(await Resolution(lotSale.Sale.Id,expired.Id))).IsSuccess,"Lote bloqueado no vuelve al inventario vendible");await c.ExecuteAsync("UPDATE inv.LotesProducto SET Activo=1 WHERE IdLote=@id;",new{id=lotSale.Lines[0].LotId});
        Saved(await Resolve(await Resolution(lotSale.Sale.Id,expired.Id)));Check((await sales.ReadAsync(lotSale.Sale.Id))!.Sale.State=="DEVUELTA_TOTAL","Devolución total deriva estado sin borrar venta");
        var oldSale=await Sale(quantity:1);await c.ExecuteAsync("UPDATE ven.Ventas SET Fecha=DATEADD(hour,-25,SYSDATETIMEOFFSET()) WHERE IdVenta=@id;",new{id=oldSale.Sale.Id});var old=(await sales.ReadAsync(oldSale.Sale.Id))!;
        Check(!(await service.RequestAsync(new(Guid.NewGuid(),old.Sale.Id,old.Sale.RowVersion,false,"Factura","Fuera de plazo",[new(old.Lines[0].Id,1,"SELLADO",true,null,[]) ]))).IsSuccess,"Hora SQL bloquea solicitudes fuera de plazo");
        var cents=await Sale(fraction,.3m);for(var i=0;i<3;i++){var part=await Request(cents.Sale.Id,.1m);Saved(await Resolve(await Resolution(cents.Sale.Id,part.Id)));}
        Check((await service.ReadAsync(cents.Sale.Id)).Returns.Sum(x=>x.Amount)==cents.Sale.Total,"Parciales decimales conservan los centavos exactos de la línea");
        var customers=new CustomerService(new SqlCustomerRepository(factory),session);var customer=(await customers.SaveAsync(new(new(){Name="Cliente posventa",TypeId=(short)(await customers.CatalogsAsync()).Types[0].Id},"Alta aislada"))).Value;
        var client=(await customers.ReadAsync(customer))!;Check((await customers.SaveCreditAsync(new(customer,client.Customer.RowVersion,new(){Enabled=true,Limit=1000,Days=15,NoticeDays=5},"Crédito aislado"))).IsSuccess,"Habilita cliente de crédito aislado");
        var creditSale=await Sale(customer:customer,onCredit:true);var account=(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Sale==creditSale.Sale.Number);
        Saved(await cash.PayAsync(new(Guid.NewGuid(),account.Id,account.RowVersion,effective,25,null,shift,"Abono antes de devolver")));
        var cr=await Request(creditSale.Sale.Id);var crRead=(await service.DetailAsync(creditSale.Sale.Id,cr.Id))!;Check(crRead.DebtReduction==5&&crRead.CashRefund==5,"Crédito descuenta deuda pendiente y reintegra sólo excedente ya pagado");
        Saved(await Resolve(await Resolution(creditSale.Sale.Id,cr.Id)));account=(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Id==account.Id);
        Check(account.Balance==0&&account.Original==30&&account.State=="PAGADA"&&(await sales.ReadAsync(creditSale.Sale.Id))!.Sale.Pending==0,"CxC salda sin reescribir monto original ni abono");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT SUM(Monto) FROM ven.AbonosCliente WHERE IdCuenta=@Id;",account)==25,"El abono original permanece íntegro");
        var raceCredit=await Sale(customer:customer,onCredit:true);var rc=await Request(raceCredit.Sale.Id);var rcAction=await Resolution(raceCredit.Sale.Id,rc.Id);
        var rcAccount=(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Sale==raceCredit.Sale.Number);var payment=new AccountPayment(Guid.NewGuid(),rcAccount.Id,rcAccount.RowVersion,effective,10,null,shift,"Abono concurrente");
        var competition=await Task.WhenAll(Resolve(rcAction),cash.PayAsync(payment));Check(competition.Any(x=>x.IsSuccess),"Abono y devolución concurrentes se serializan sin inversión de bloqueos");
        if(!competition[0].IsSuccess)Saved(await Resolve(await Resolution(raceCredit.Sale.Id,rc.Id)));
        if(!competition[1].IsSuccess){rcAccount=(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Id==rcAccount.Id);Saved(await cash.PayAsync(payment with{OperationId=Guid.NewGuid(),ExpectedVersion=rcAccount.RowVersion}));}
        rcAccount=(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Id==rcAccount.Id);Check(rcAccount.Balance==10,"Abono y reducción de deuda se aplican exactamente una vez cada uno");
        var cancelSale=await Sale(quantity:2,delivery:3);var cancel=await Request(cancelSale.Sale.Id,2,cancel:true);var cancelRead=(await service.DetailAsync(cancelSale.Sale.Id,cancel.Id))!;Check(cancelRead.CurrentAmount==23,"Anulación completa incluye cargo de entrega aún no prestado");Saved(await Resolve(await Resolution(cancelSale.Sale.Id,cancel.Id)));
        Check((await sales.ReadAsync(cancelSale.Sale.Id))!.Sale.State=="ANULADA"&&(await service.DetailAsync(cancelSale.Sale.Id,cancel.Id))!.Return.CashRefund==23,"Anulación compensa todo y conserva venta como anulada");
        var orders=new OrderService(repo,session);
        foreach(var finalState in new[]{"LISTO","EN_RUTA","ENTREGADO"})
        {
            var orderId=Saved(await orders.PlaceAsync(new(Guid.NewGuid(),customer,"Contacto","0000","Dirección de prueba",null,null,null,null,null,null,3,[new(p,2)],"Pedido para posventa")));
            var linkedSale=await Sale(quantity:2,customer:customer,delivery:3);var order=(await orders.ReadAsync(orderId))!;
            Saved(await orders.LinkSaleAsync(new(Guid.NewGuid(),orderId,order.Order.RowVersion,linkedSale.Sale.Id,"Vincular antes de reparto")));
            foreach(var state in new[]{"PREPARANDO","LISTO","EN_RUTA","ENTREGADO"})
            {order=(await orders.ReadAsync(orderId))!;Saved(await orders.TransitionAsync(new(Guid.NewGuid(),orderId,order.Order.RowVersion,state,"Seguimiento aislado")));if(state==finalState)break;}
            var deliveredAt=await c.ExecuteScalarAsync<DateTimeOffset?>("SELECT FechaEntrega FROM ven.PedidosMvp WHERE IdPedido=@orderId;",new{orderId});
            var req=await Request(linkedSale.Sale.Id,2,cancel:true);Saved(await Resolve(await Resolution(linkedSale.Sale.Id,req.Id)));
            order=(await orders.ReadAsync(orderId))!;Check(order.Order.State=="CANCELADO"&&order.Order.SaleId==linkedSale.Sale.Id,$"Anulación desde {finalState} conserva vínculo y cancela pedido");
            Check(await c.ExecuteScalarAsync<DateTimeOffset?>("SELECT FechaEntrega FROM ven.PedidosMvp WHERE IdPedido=@orderId;",new{orderId})==deliveredAt,"Anulación conserva fecha histórica de entrega");
            Check(order.Events.Any(e=>e.Reason.Contains("Posventa")),"Pedido registra evento de anulación sin borrar eventos anteriores");
        }
        var rollbackSale=await Sale(quantity:1);var rollback=await Request(rollbackSale.Sale.Id);var action=await Resolution(rollbackSale.Sale.Id,rollback.Id);var before=(await cash.ReadAsync(shift))!.Expected;
        await c.ExecuteAsync("ALTER TABLE sis.Bitacora WITH NOCHECK ADD CONSTRAINT CK_TestReturnAudit CHECK(Accion<>N'APLICAR_DEVOLUCION');");var failed=false;
        try{await Resolve(action);}catch(Microsoft.Data.SqlClient.SqlException ex)when(ex.Number==547){failed=true;}finally{await c.ExecuteAsync("ALTER TABLE sis.Bitacora DROP CONSTRAINT CK_TestReturnAudit;");}
        Check(failed&&(await service.DetailAsync(rollbackSale.Sale.Id,rollback.Id))!.Return.State=="SOLICITADA"&&(await cash.ReadAsync(shift))!.Expected==before,"Fallo de auditoría revierte solicitud, caja e inventario juntos");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE ReferenciaTipo=N'DEVOLUCION' AND ReferenciaId=@id;",new{id=rollback.Id})==0,"Rollback no deja Kardex huérfano");Saved(await Resolve(action));
        var closedSale=await Sale(quantity:1);var closedRequest=await Request(closedSale.Sale.Id);var closedAction=await Resolution(closedSale.Sale.Id,closedRequest.Id);var closing=(await cash.ReadAsync(shift))!;
        Saved(await cash.CloseAsync(new(Guid.NewGuid(),shift,closing.RowVersion,closing.Expected,"Cerrar turno antes de reintegrar")));
        Check(!(await Resolve(closedAction)).IsSuccess,"Turno cerrado no recibe un reintegro ni altera la devolución");
        shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,0,"Turno sin fondo")));Check(!(await Resolve(await Resolution(closedSale.Sale.Id,closedRequest.Id))).IsSuccess,"Efectivo insuficiente bloquea la aplicación completa");
        var emptyShift=(await cash.ReadAsync(shift))!;Saved(await cash.MoveAsync(new(Guid.NewGuid(),shift,emptyShift.RowVersion,"INGRESO",10,"Fondo para reintegro")));Saved(await Resolve(await Resolution(closedSale.Sale.Id,closedRequest.Id)));
        var denied=await c.ExecuteScalarAsync<int>("INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,Activo,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(N'return_denied',N'Sin permiso',N'no-login',1,0);");
        Check((await repo.ResolveReturnAsync(await Resolution(first.Sale.Id,stale.Id,false),denied,identity.Id,identity.WorkstationId,default)).ErrorCode=="FORBIDDEN","SQL exige permiso y administrador vigente al resolver");
        var latest=(await sales.ReadAsync(first.Sale.Id))!;Check((await repo.RequestReturnAsync(requested.Request with{OperationId=Guid.NewGuid(),ExpectedSaleVersion=latest.Sale.RowVersion},denied,identity.WorkstationId,default)).ErrorCode=="FORBIDDEN","SQL revalida permiso de solicitud");
        var version=applied.Return.RowVersion;await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));Check((await service.DetailAsync(first.Sale.Id,requested.Id))!.Return.RowVersion.SequenceEqual(version),"Reaplicar 019 conserva devolución, autorización y compensaciones");
        Check((await service.EvidenceLocationAsync()).Root==evidenceRoot&&await evidence.VerifyAsync(photo,identity.Id),"Reaplicar migración conserva raíz y fotos verificables");
        Directory.Delete(evidenceRoot,true);
        Console.WriteLine($"{checks} comprobaciones de posventa SQL correctas.");
    }
}
