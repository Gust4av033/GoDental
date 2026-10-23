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
internal static class FiscalIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("OK "+name);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException(r.ErrorCode+": "+r.Message);
        await using var c=await factory.OpenConnectionAsync();var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());
        var identity=(await gateway.CreateAdministratorAsync("fiscal_test","Administrador fiscal aislado",Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),"FISCAL-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var repo=new SqlMvpRepository(factory);var fiscal=new FiscalWorkspaceService(repo,session);var pending=new FiscalPendingService(repo,session);
        var products=new ProductService(new SqlProductRepository(factory),session);var catalogs=(await products.GetCatalogsAsync()).Value!;
        var product=(int)(await products.SaveAsync(new(){Sku="FISCAL-TEST",Name="Producto fiscal sintético",CategoryId=catalogs.Categories[0].Id,UnitId=(short)catalogs.Units.First(u=>u.Name.Contains("Unidad")).Id,Price=10})).Value;
        var warehouse=await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        Saved(await new InventorySetupService(repo,session).InitializeAsync(new(Guid.NewGuid(),product,warehouse,50,3,"Saldo aislado")));
        var customers=new CustomerService(new SqlCustomerRepository(factory),session);var type=(short)(await customers.CatalogsAsync()).Types[0].Id;
        var customer=(await customers.SaveAsync(new(new(){Name="Receptor original sintético",TypeId=type},"Alta aislada"))).Value;
        var cash=new CashService(repo,session);var box=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"FISCAL-CAJA","Caja fiscal aislada",warehouse,true,"Alta aislada")));
        var shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,100,"Apertura aislada")));var sales=new SalesService(repo,session,gateway);
        var method=(await sales.PaymentMethodsAsync()).Single(x=>x.Code=="EFECTIVO").Id;
        async Task<long> Sale(){var cart=new SaleCart(box,customer,[new(product,1)]);var q=await sales.QuoteAsync(cart);return Saved(await sales.ConfirmAsync(new(Guid.NewGuid(),cart,q.Token,[new(method,q.Total)],q.Total,shift,"Venta fiscal aislada")));}
        async Task<FiscalDetail> Detail(long id)=>(await fiscal.DetailAsync(id))!;
        ReviewFiscalInternal Review(FiscalDetail d,string outcome="REVISADO")=>new(Guid.NewGuid(),d.Document.Id,d.Document.RowVersion,d.ReviewToken,outcome,"Revisión sintética");
        var sale=await Sale();var sale2=await Sale();var original=(await sales.ReadAsync(sale))!;
        Check((await fiscal.CandidatesAsync(original.Sale.Number,new())).Items.Any(x=>x.Id==sale),"Fiscal busca venta confirmada por número");
        var request=new PendingFiscal(Guid.NewGuid(),sale,"FE","Preparar internamente");var id=Saved(await pending.RegisterAsync(request));
        Check(Saved(await pending.RegisterAsync(request))==id,"Preparación reenvía el mismo GUID sin duplicar");
        Check(Saved(await pending.RegisterAsync(request with{OperationId=Guid.NewGuid()}))==id,"Venta conserva una solicitud fiscal activa");
        Check(!(await pending.RegisterAsync(request with{OperationId=Guid.NewGuid(),Type="CCFE"})).IsSuccess,"No sustituye solicitud activa por otro tipo");
        Check(!(await fiscal.CandidatesAsync("",new())).Items.Any(x=>x.Id==sale),"Solicitud activa excluye venta de candidatas");
        Check((await fiscal.SearchAsync(original.Sale.Number,"SIN_REVISAR",new())).Items.Single().Id==id,"Bandeja filtra venta y revisión pendiente");
        var d=await Detail(id);Check(d.Issues.Count==0&&d.Document.State=="PENDIENTE"&&d.Receiver.Name.Contains("original")&&d.Lines.Count==1,"Ficha consulta venta, receptor y líneas sin payload fiscal");
        var stock=await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@product AND IdAlmacen=@warehouse;",new{product,warehouse});var expected=(await cash.ReadAsync(shift))!.Expected;
        var review=Review(d);Saved(await fiscal.ReviewAsync(review));Check(Saved(await fiscal.ReviewAsync(review))==id,"Revisión recuperable no duplica escritura");d=await Detail(id);
        Check(d.Document.ReviewState=="REVISADO"&&d.ReviewCurrent&&d.Document.State=="PENDIENTE","Revisar no genera DTE ni cambia estado fiscal");
        Check((await fiscal.HistoryAsync(id,new())).Total==1,"Reenvío conserva un único evento de revisión");
        Check((await fiscal.SearchAsync("","REVISADO",new(1,1))).Total==1&&(await fiscal.SearchAsync("","REVISADO",new(2,1))).Items.Count==0,"Bandeja pagina sin duplicar filas");
        var stale=Review(d);await c.ExecuteAsync("UPDATE cli.Clientes SET NombreRazonSocial=N'Receptor actualizado sintético' WHERE IdCliente=@customer;",new{customer});d=await Detail(id);
        Check(!d.ReviewCurrent&&d.Receiver.Name.Contains("actualizado")&&d.Sale.CustomerSnapshot==original.Sale.CustomerSnapshot,"Receptor actualizado invalida revisión conservando comprobante original");
        Check((await fiscal.ReviewAsync(stale)).ErrorCode=="CONCURRENCY_CONFLICT","Cambio de receptor invalida token aun sin cambiar versión fiscal");
        var race=await Task.WhenAll(fiscal.ReviewAsync(Review(d)),fiscal.ReviewAsync(Review(d)));
        Check(race.Count(x=>x.IsSuccess)==1,"Dos estaciones no sobrescriben la misma versión");
        var beforeFailure=await Detail(id);var bad=Review(beforeFailure,"OBSERVADO");
        await c.ExecuteAsync("CREATE TRIGGER sis.TR_TestFiscalAudit ON sis.Bitacora AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion=N'REVISAR_FISCAL_INTERNO') THROW 50991,N'Fallo aislado de auditoría',1; END;");
        var failed=false;try{await fiscal.ReviewAsync(bad);}catch(SqlException ex)when(ex.Number==50991){failed=true;}finally{await c.ExecuteAsync("DROP TRIGGER sis.TR_TestFiscalAudit;");}
        Check(failed&&(await Detail(id)).Document.RowVersion.SequenceEqual(beforeFailure.Document.RowVersion),"Fallo de auditoría revierte metadatos y versión");
        Check(await repo.FindOperationAsync(bad.OperationId,identity.Id,default)is null,"Rollback no deja recibo confirmado");
        Check((await repo.ResolveOperationAsync(OperationIdentity.Describe(bad),true,identity.Id,identity.WorkstationId,default)).State==OperationResolutionState.RejectedDefinitively,"Revisión incierta ausente puede cerrarse sin efectos");
        Check((await fiscal.ReviewAsync(bad)).ErrorCode=="OPERATION_REJECTED","Solicitud cerrada no se confirma tardíamente");
        var id2=Saved(await pending.RegisterAsync(new(Guid.NewGuid(),sale2,"FE","Segunda solicitud")));await c.ExecuteAsync("UPDATE fis.DocumentosFiscales SET TipoDocumento=N'CCFE' WHERE IdDocumentoFiscal=@id2;",new{id2});
        var credit=await Detail(id2);Check(credit.Issues.Count>0,"CCFE detecta datos fiscales incompletos");
        Check(!(await fiscal.ReviewAsync(Review(credit))).IsSuccess,"CCFE incompleto no admite resultado revisado");
        Saved(await fiscal.ReviewAsync(Review(credit,"OBSERVADO")));Check((await Detail(id2)).Document.ReviewState=="OBSERVADO","Se permite registrar observación sin fingir validación fiscal");
        await c.ExecuteAsync("UPDATE cli.Clientes SET NIT=N'00000000-0',NRC=N'123456-7' WHERE IdCliente=@customer; INSERT INTO cli.ClienteDirecciones(IdCliente,Tipo,Direccion,EsPrincipal) VALUES(@customer,N'FISCAL',N'Dirección fiscal sintética',1);",new{customer});
        credit=await Detail(id2);Check(credit.Issues.Count==0&&credit.Receiver.Address!.Contains("sintética"),"CCFE consulta NIT NRC y dirección fiscal activa");
        Saved(await fiscal.ReviewAsync(Review(credit)));credit=await Detail(id2);
        await c.ExecuteAsync("UPDATE cli.ClienteDirecciones SET Direccion=N'Dirección fiscal cambiada' WHERE IdCliente=@customer AND Tipo=N'FISCAL';",new{customer});
        Check(!(await Detail(id2)).ReviewCurrent&&(await fiscal.ReviewAsync(Review(credit))).ErrorCode=="CONCURRENCY_CONFLICT","Cambiar dirección fiscal invalida revisión y token");
        await c.ExecuteAsync("UPDATE cli.Clientes SET Activo=0 WHERE IdCliente=@customer;",new{customer});
        Check(!(await fiscal.ReviewAsync(Review(await Detail(id2)))).IsSuccess,"CCFE exige cliente activo al revisar");
        await c.ExecuteAsync("UPDATE cli.Clientes SET Activo=1 WHERE IdCliente=@customer;",new{customer});
        Saved(await fiscal.ReviewAsync(Review(await Detail(id2))));
        await c.ExecuteAsync("UPDATE fis.DocumentosFiscales SET TipoDocumento=N'FE' WHERE IdDocumentoFiscal=@id2;",new{id2});
        Check(!(await Detail(id2)).ReviewCurrent,"Cambio de tipo documental invalida revisión sin auto invalidarse al guardarla");
        // Fixture SQL aislado: comprueba guardas de integración, sin simular una respuesta oficial.
        await c.ExecuteAsync("INSERT INTO fis.DocumentosDte(IdDocumentoFiscal,TipoDte,Estado) VALUES(@id2,N'TEST',N'PRUEBA');",new{id2});
        Check(!(await fiscal.ReviewAsync(Review(await Detail(id2),"OBSERVADO"))).IsSuccess,"DTE asociado bloquea revisión interna independiente");
        await c.ExecuteAsync("DELETE FROM fis.DocumentosDte WHERE IdDocumentoFiscal=@id2; UPDATE ven.Ventas SET Estado=N'ANULADA' WHERE IdVenta=@sale2;",new{id2,sale2});
        Check(!(await fiscal.ReviewAsync(Review(await Detail(id2)))).IsSuccess,"Venta anulada no se marca revisada");
        Check(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fis.DocumentosDte;")==0&&await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fis.EventosDte;")==0,"Recorrido interno no crea DTE ni eventos técnicos");
        Check((await cash.ReadAsync(shift))!.Expected==expected&&await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@product AND IdAlmacen=@warehouse;",new{product,warehouse})==stock,"Revisión no mueve caja ni inventario");
        var version=(await Detail(id)).Document.RowVersion;await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));
        Check((await Detail(id)).Document.RowVersion.SequenceEqual(version)&&(await fiscal.HistoryAsync(id,new())).Total==2,"Reaplicar 024 conserva revisiones, versiones y auditoría");
        var constraint=false;try{await c.ExecuteAsync("UPDATE fis.DocumentosFiscales SET ObservacionRevision=NULL WHERE IdDocumentoFiscal=@id;",new{id});}catch(SqlException ex)when(ex.Number==547){constraint=true;}Check(constraint,"SQL exige motivo no nulo en revisión");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'FISCAL.REVISAR_INTERNO');");
        Check((await fiscal.ReviewAsync(Review(await Detail(id)))).ErrorCode=="FORBIDDEN","SQL revalida permiso revocado pese a sesión en memoria");
        await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));Check((await fiscal.ReviewAsync(Review(await Detail(id)))).ErrorCode=="FORBIDDEN","Reaplicar no restaura permisos revocados");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'FISCAL.VER');");
        var denied=false;try{await fiscal.DetailAsync(id);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"SQL bloquea lectura fiscal sin permiso actual");
        Console.WriteLine($"Fiscal interno: {checks} comprobaciones correctas. Base aislada retenida: {database}");
    }
}
