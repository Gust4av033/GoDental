using System.Security.Cryptography;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Operations;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
internal static class ImportIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;Console.WriteLine("OK "+message);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException($"{r.ErrorCode}: {r.Message}");
        await using var c=await factory.OpenConnectionAsync();
        var identity=(await new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher()).CreateAdministratorAsync("imports_test","Administrador de prueba",Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),"IMPORTS-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var repo=new SqlMvpRepository(factory);var purchases=new PurchaseService(repo,session);var imports=new ImportService(repo,session);
        var supplier=(await new SupplierService(new SqlSupplierRepository(factory),session).SaveAsync(new(new(){Name="Proveedor de importaciones de prueba"},"Alta aislada"))).Value;
        var products=new ProductService(new SqlProductRepository(factory),session);var cat=(await products.GetCatalogsAsync()).Value!;
        async Task<int> Product(string sku,bool lot=false)
        {
            var r=await products.SaveAsync(new(){Sku=sku,Name="Producto "+sku,CategoryId=cat.Categories[0].Id,UnitId=(short)cat.Units.First(u=>u.Name.Contains("Unidad")).Id,Price=30,TracksLot=lot,TracksExpiry=lot});
            if(!r.IsSuccess)throw new InvalidOperationException(r.Message);return checked((int)r.Value);
        }
        var businessDate=await c.ExecuteScalarAsync<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        var p=await Product("IMP-A");var q=await Product("IMP-B",true);var catalogs=await purchases.CatalogsAsync();var usd=catalogs.Currencies.Single(x=>x.Code=="USD").Id;var warehouse=catalogs.Warehouses.First().Id;
        async Task<PurchaseOrderSnapshot> Read(long id)=>(await purchases.ReadAsync(id))!;
        async Task<ImportationSnapshot> Import(long id)=>(await imports.ReadAsync(id))!;
        async Task<PurchaseOrderSnapshot> Order(IReadOnlyList<PurchaseLine>? lines=null)
        {
            var id=Saved(await purchases.SaveAsync(new(Guid.NewGuid(),0,[],supplier,usd,1,businessDate,lines??[new(p,3,10),new(q,3,20)],"OC de prueba")));
            var x=await Read(id);Saved(await purchases.TransitionAsync(new(Guid.NewGuid(),id,x.Order.RowVersion,"CONFIRMADA","Confirmar OC")));return await Read(id);
        }
        var order=await Order();var create=new SaveImportation(Guid.NewGuid(),order.Order.Id,order.Order.RowVersion,null,null,null,"Importación manual");
        var id=Saved(await imports.SaveAsync(create));Check((await Import(order.Order.Id)).Importation.Guide is null,"Guía y courier pueden quedar vacíos");
        Check(Saved(await imports.SaveAsync(create))==id,"Reenvío de creación conserva la importación");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario")==0,"Crear importación no ingresa stock");
        order=await Read(order.Order.Id);
        Check(!(await imports.SaveAsync(create with{OperationId=Guid.NewGuid(),ExpectedOrderVersion=order.Order.RowVersion})).IsSuccess,"No crea otra importación para la misma OC");
        var imp=await Import(order.Order.Id);
        Check(!(await purchases.PrepareAsync(new(Guid.NewGuid(),order.Order.Id,order.Order.RowVersion,warehouse,1,true,[new(order.Lines[0].Id,1)],"Ruta directa"))).IsSuccess,"La ruta de compra directa no omite el cierre de importación");
        Check(!(await imports.SaveAsync(new PrepareImportReceipt(Guid.NewGuid(),id,imp.Importation.RowVersion,new(Guid.NewGuid(),order.Order.Id,order.Order.RowVersion,warehouse,1,true,[new(order.Lines[0].Id,1)],"Sin cierre")))).IsSuccess,"Identidades distintas no preparan una recepción");
        async Task<ImportExpense> Expense(decimal amount=1.01m,short? currency=null,decimal fx=1)
        {var x=await Import(order.Order.Id);return new(Guid.NewGuid(),order.Order.Id,id,x.Importation.RowVersion,"FLETE",amount,currency??usd,fx,DateTime.Today,"Flete de prueba","Gasto aislado");}
        var expense=await Expense();Saved(await imports.SaveAsync(expense));Check((await Import(order.Order.Id)).ActiveExpensesUsd==1.01m,"Conserva gasto y cambio histórico en USD");
        Check(!(await imports.SaveAsync(expense with{OperationId=Guid.NewGuid()})).IsSuccess,"Versión antigua no duplica un gasto");
        var invalidUsd=await Expense(1,usd,2);Check(!(await imports.SaveAsync(invalidUsd)).IsSuccess,"USD exige tipo de cambio uno");
        var extra=await Expense(2);Saved(await imports.SaveAsync(extra));imp=await Import(order.Order.Id);var voidId=imp.Expenses.Single(x=>x.AmountUsd==2).Id;
        Saved(await imports.SaveAsync(new VoidImportExpense(Guid.NewGuid(),order.Order.Id,id,imp.Importation.RowVersion,voidId,"Gasto incorrecto")));
        imp=await Import(order.Order.Id);Check(imp.Expenses.Count==2&&imp.Expenses.Single(x=>x.Id==voidId).Voided&&imp.ActiveExpensesUsd==1.01m,"Anular conserva historial y excluye del total");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT GastosImportacionUSD FROM cmp.vwImportacionesSeguimiento WHERE IdImportacion=@id",new{id})==1.01m,"Vista de seguimiento excluye gastos anulados");
        var log=new ImportEvent(Guid.NewGuid(),order.Order.Id,id,imp.Importation.RowVersion,"DESPACHADA","Courier de prueba",null,DateTime.Today.AddDays(4),"Despacho manual");Saved(await imports.SaveAsync(log));
        Check(Saved(await imports.SaveAsync(log))==id,"Reenvío no duplica eventos");imp=await Import(order.Order.Id);
        Check(imp.Events.Any(e=>e.Description.Contains("Despacho manual"))&&imp.Importation.State=="DESPACHADA","Logística crea historial sin exigir tracking externo");
        Check(!(await imports.SaveAsync(log with{OperationId=Guid.NewGuid(),ExpectedVersion=imp.Importation.RowVersion,State="RECIBIDA"})).IsSuccess,"Un evento no puede simular recepción");
        var close=new CloseImportCosts(Guid.NewGuid(),order.Order.Id,id,imp.Importation.RowVersion,1,"Todos los gastos conocidos");Saved(await imports.SaveAsync(close));Check(Saved(await imports.SaveAsync(close))==id,"Cierre es recuperable por el mismo identificador");
        imp=await Import(order.Order.Id);order=await Read(order.Order.Id);
        Check(imp.Importation.CostsClosed&&order.Lines.Sum(l=>l.ImportExpenseUsd)==1.01m,"Cierre congela reparto exacto de centavos");
        Check(order.Lines[0].ImportExpenseUsd==.34m&&order.Lines[1].ImportExpenseUsd==.67m,"Reparto utiliza valor neto y restos mayores");
        Check(!(await imports.SaveAsync((await Expense(1)))).IsSuccess,"No agrega gastos posteriores al cierre");
        Check(!(await imports.SaveAsync(new VoidImportExpense(Guid.NewGuid(),order.Order.Id,id,imp.Importation.RowVersion,imp.Expenses.First(x=>!x.Voided).Id,"Intentar reabrir"))).IsSuccess,"No anula gastos de un costo cerrado");
        async Task<PurchaseReceiptSnapshot> Prepare(IReadOnlyList<ReceivePurchaseLine> lines)
        {
            order=await Read(order.Order.Id);var x=await Import(order.Order.Id);var op=Guid.NewGuid();
            var r=new PrepareImportReceipt(op,id,x.Importation.RowVersion,new(op,order.Order.Id,order.Order.RowVersion,warehouse,1,true,lines,"Recepción física de prueba"));
            var receipt=Saved(await imports.SaveAsync(r));Check(Saved(await imports.SaveAsync(r))==receipt,"Preparación se reenvía sin duplicar borrador");return(await purchases.ReceiptAsync(receipt))!;
        }
        async Task<PurchaseReceiptAction> Apply(PurchaseReceiptSnapshot r)=>new(Guid.NewGuid(),r.Receipt.Id,r.Receipt.RowVersion,(await Read(order.Order.Id)).Order.RowVersion,true,"Aplicar recepción de prueba");
        var first=await Prepare([new(order.Lines[0].Id,1),new(order.Lines[1].Id,1,LotNumber:"L-1",Expires:DateTime.Today.AddYears(1))]);
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario")==0,"Cerrar y preparar no ponen tránsito a la venta");
        var apply=await Apply(first);Saved(await purchases.ApplyAsync(apply));Check(Saved(await purchases.ApplyAsync(apply))==first.Receipt.Id,"Aplicación no duplica stock en reenvío");
        imp=await Import(order.Order.Id);order=await Read(order.Order.Id);
        Check(imp.Importation.State=="RECIBIDA_PARCIAL"&&order.Lines.All(l=>l.Received==1),"Primera recepción deriva estado parcial y cantidades");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT SUM(d.GastoImportacionTotalUSD) FROM cmp.RecepcionCompraDetalle d JOIN cmp.RecepcionesCompra r ON r.IdRecepcion=d.IdRecepcion WHERE r.Estado=N'APLICADA'")==.33m,"Primera parcial consume su presupuesto acumulado redondeado");
        var second=await Prepare([new(order.Lines[0].Id,2),new(order.Lines[1].Id,1,LotNumber:"L-2",Expires:DateTime.Today.AddYears(1)),new(order.Lines[1].Id,1,LotNumber:"L-3",Expires:DateTime.Today.AddYears(1))]);
        var race=await Apply(second);var results=await Task.WhenAll(purchases.ApplyAsync(race),purchases.ApplyAsync(race with{OperationId=Guid.NewGuid()}));Check(results.Count(x=>x.IsSuccess)==1,"Dos estaciones no aplican la misma recepción dos veces");
        imp=await Import(order.Order.Id);Check(imp.Importation.State=="RECIBIDA"&&imp.Importation.ReceivedDate is not null,"Recepción completa deriva estado y fecha");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT SUM(d.GastoImportacionTotalUSD) FROM cmp.RecepcionCompraDetalle d JOIN cmp.RecepcionesCompra r ON r.IdRecepcion=d.IdRecepcion WHERE r.Estado=N'APLICADA'")==1.01m,"Último parcial consume remanente exacto sin duplicación");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT SUM(StockActual) FROM inv.ExistenciasProducto")==6,"Cantidades físicas actualizan existencias");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.LotesProducto WHERE IdProducto=@q",new{q})==3,"Varios lotes conservan origen por recepción");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM inv.MovimientosInventario WHERE TipoMovimiento=N'COMPRA'")==5,"Kardex conserva todas las entradas por línea");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cmp.RecepcionCompraDetalle WHERE CostoPuestoBodega<>CostoCompraUSD+GastoAsignadoUSD")==0,"Costos de recepción reconcilian a seis decimales");
        var closedVersion=imp.Importation.RowVersion.ToArray();await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));imp=await Import(order.Order.Id);
        Check(imp.Importation.RowVersion.SequenceEqual(closedVersion)&&imp.ActiveExpensesUsd==1.01m,"Reaplicar 018 preserva costos, versión y gastos");
        Check(await c.ExecuteScalarAsync<decimal>("SELECT CostoPromedio FROM inv.ExistenciasProducto WHERE IdProducto=@p AND IdAlmacen=@warehouse",new{p,warehouse})==10.113333m,"Promedio conserva costo de mercancía y gastos de los parciales");
        var rollbackOrder=await Order([new(p,2,5)]);Saved(await imports.SaveAsync(new SaveImportation(Guid.NewGuid(),rollbackOrder.Order.Id,rollbackOrder.Order.RowVersion,null,null,null,"Prueba de cierre")));
        var rollbackImport=await Import(rollbackOrder.Order.Id);var cny=catalogs.Currencies.Single(x=>x.Code=="CNY").Id;
        var fxExpense=new ImportExpense(Guid.NewGuid(),rollbackOrder.Order.Id,rollbackImport.Importation.Id,rollbackImport.Importation.RowVersion,"SEGURO",10,cny,.14m,DateTime.Today,"Seguro","Cambio histórico");
        Saved(await imports.SaveAsync(fxExpense));rollbackImport=await Import(rollbackOrder.Order.Id);Check(rollbackImport.ActiveExpensesUsd==1.4m,"Conversión de gasto usa cambio histórico y centavos");
        var rollbackClose=new CloseImportCosts(Guid.NewGuid(),rollbackOrder.Order.Id,rollbackImport.Importation.Id,rollbackImport.Importation.RowVersion,1,"Cierre para rollback");
        await c.ExecuteAsync("ALTER TABLE sis.Bitacora WITH NOCHECK ADD CONSTRAINT CK_TestImportAudit CHECK(Accion<>N'CERRAR_COSTOS_IMPORTACION');");
        var failed=false;try{await imports.SaveAsync(rollbackClose);}catch(Microsoft.Data.SqlClient.SqlException ex)when(ex.Number==547){failed=true;}
        finally{await c.ExecuteAsync("ALTER TABLE sis.Bitacora DROP CONSTRAINT CK_TestImportAudit;");}
        Check(failed&&!(await Import(rollbackOrder.Order.Id)).Importation.CostsClosed&&(await Read(rollbackOrder.Order.Id)).Lines.All(l=>l.ImportExpenseUsd is null),"Fallo de auditoría revierte cierre y presupuestos completos");
        Check((await Import(rollbackOrder.Order.Id)).Importation.RowVersion.SequenceEqual(rollbackImport.Importation.RowVersion),"Rollback conserva versión de importación");
        Saved(await imports.SaveAsync(rollbackClose));Check((await Import(rollbackOrder.Order.Id)).Importation.CostsClosed,"Reintento del cierre tras rollback se confirma una vez");
        var other=await c.ExecuteScalarAsync<int>("INSERT INTO seg.Usuarios(Usuario,NombreCompleto,PasswordHash,Activo,DebeCambiarClave) OUTPUT INSERTED.IdUsuario VALUES(N'import_denied',N'Sin permiso',N'no-login',1,0);");
        Check((await repo.WriteImportAsync(close with{OperationId=Guid.NewGuid()},other,identity.WorkstationId,default)).ErrorCode=="FORBIDDEN","SQL revalida permiso de escritura de importación");
        var denied=false;try{await repo.ReadImportAsync(order.Order.Id,new(),other,default);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"SQL protege importes e historial de importación");
        var found=await repo.ResolveOperationAsync(OperationIdentity.Describe(close),false,identity.Id,identity.WorkstationId,default);Check(found.State==OperationResolutionState.Confirmed,"Recuperación durable encuentra el cierre confirmado");
        session.Clear();Check(!(await imports.SaveAsync(close)).IsSuccess,"Servicio deniega sin sesión");session.Begin(identity);
        await c.ExecuteAsync("UPDATE sis.EquiposTrabajo SET Activo=0 WHERE IdEquipo=@id",new{id=identity.WorkstationId});imp=await Import(order.Order.Id);
        Check(!(await imports.SaveAsync(log with{OperationId=Guid.NewGuid(),ExpectedVersion=imp.Importation.RowVersion,State="RECIBIDA"})).IsSuccess,"SQL exige estación activa incluso con sesión abierta");
        await c.ExecuteAsync("UPDATE sis.EquiposTrabajo SET Activo=1 WHERE IdEquipo=@id",new{id=identity.WorkstationId});
        Console.WriteLine($"{checks} comprobaciones de importaciones SQL correctas.");
    }
}
