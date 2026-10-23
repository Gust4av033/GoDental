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
internal static class OnlineIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks++;Console.WriteLine("OK "+name);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException(r.ErrorCode+": "+r.Message);
        await using var c=await factory.OpenConnectionAsync();var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());
        var identity=(await gateway.CreateAdministratorAsync("online_test","Administrador Online aislado",Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),"ONLINE-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var repo=new SqlMvpRepository(factory);var online=new OnlineService(repo,session);
        var today=(await c.ExecuteScalarAsync<DateTimeOffset>("SELECT SYSDATETIMEOFFSET();")).ToOffset(TimeSpan.FromHours(-6)).Date;
        OnlineSourceInput Source(string path="contacto")=>new("WEB","https://fuente.example.com/"+path,today,"Datos empresariales sintéticos");
        SaveOnlineProspect New(string name,string? phone=null,string? email=null,string? website=null)=>new(Guid.NewGuid(),0,[],new(){Name=name,Phone=phone,Email=email,Website=website},Source(),"Alta aislada");
        Task<OnlineProspectData?> Read(long id)=>online.ReadAsync(id);
        var r=New("Negocio Uno","70000000","uno@example.com","https://uno.example.com/");var id=Saved(await online.SaveAsync(r));
        Check(Saved(await online.SaveAsync(r))==id,"Alta Online reenvía mismo GUID sin duplicar ficha o fuente");
        Check((await online.SourcesAsync(id,new())).Total==1,"Fuente inicial se guarda atómicamente con prospecto");
        var p=(await Read(id))!;Check(p.Created is not null&&p.State=="NUEVO"&&!p.Suppressed,"Ficha nueva no inventa consentimiento ni contacto");
        Check((await online.SearchAsync("+503 7000-0000",null,new())).Items.Single().Id==id,"Búsqueda reconoce teléfono nacional con prefijo");
        Check((await online.SaveAsync(New("Otro nombre","+503 7000-0000"))).ErrorCode=="DUPLICATE","Teléfono y WhatsApp equivalentes evitan prospecto duplicado");
        Check((await online.SaveAsync(New("Otro correo",email:"UNO@EXAMPLE.COM"))).ErrorCode=="DUPLICATE","Correo compara mayúsculas y espacios");
        Check((await online.SaveAsync(New("Otro sitio",website:"https://www.uno.example.com/otra"))).ErrorCode=="DUPLICATE","Dominio del negocio evita duplicar por otra ruta");
        var other=Saved(await online.SaveAsync(New("Negocio Dos","71111111",website:"https://dos.example.com/")));
        Check(other!=id,"Misma fuente de procedencia admite dos negocios distintos");
        var social=New("Negocio Social","72222222") with{Data=new(){Name="Negocio Social",Instagram="https://instagram.com/negocio/"}};Saved(await online.SaveAsync(social));
        Check((await online.SaveAsync(New("Social repetido") with{Data=new(){Name="Social repetido",Instagram="https://www.instagram.com/NEGOCIO"}})).ErrorCode=="DUPLICATE","Perfil social normaliza dominio y barra final");
        var location=new OnlineProspectInput{Name="Clínica Ubicada",Department="San Salvador",Municipality="San Salvador Centro"};Saved(await online.SaveAsync(New(location.Name) with{Data=location}));
        Check((await online.SaveAsync(New("CLINICA   UBICADA") with{Data=location with{Name="CLINICA   UBICADA"}})).ErrorCode=="DUPLICATE","Nombre y municipio ignoran acentos y espacios repetidos");
        var raceRequest=New("Negocio concurrente","73333333");var race=await Task.WhenAll(online.SaveAsync(raceRequest),online.SaveAsync(raceRequest with{OperationId=Guid.NewGuid()}));
        Check(race.Count(x=>x.IsSuccess)==1&&race.Any(x=>x.ErrorCode=="DUPLICATE"),"Dos estaciones no crean dos fichas coincidentes");
        var sourceRequest=new AddOnlineSource(Guid.NewGuid(),id,p.RowVersion,Source("otra"),"Fuente adicional");Saved(await online.SaveAsync(sourceRequest));Saved(await online.SaveAsync(sourceRequest));
        Check((await online.SourcesAsync(id,new(1,1))).Total==2&&(await online.SourcesAsync(id,new(2,1))).Items.Count==1,"Fuentes inmutables paginan y reenvío no duplica");
        Check((await online.SaveAsync(new ChangeOnlineState(Guid.NewGuid(),id,p.RowVersion,"REVISADO","Versión anterior"))).ErrorCode=="CONCURRENCY_CONFLICT","Fuente adicional invalida versión anterior");
        p=(await Read(id))!;Saved(await online.SaveAsync(new ChangeOnlineState(Guid.NewGuid(),id,p.RowVersion,"REVISADO","Datos revisados")));
        p=(await Read(id))!;var block=new BlockOnlineProspect(Guid.NewGuid(),id,p.RowVersion,"Solicitud de no contactar");Saved(await online.SaveAsync(block));Saved(await online.SaveAsync(block));p=(await Read(id))!;
        Check(p.DoNotContact&&p.Suppressed,"Bloqueo global persiste en ficha");
        Check((await online.ExclusionsAsync("+50370000000",new())).Total==1&&(await online.ExclusionsAsync("uno@example.com",new())).Total==1,"Bloqueo agrega teléfonos y correo a exclusión central");
        var changed=new SaveOnlineProspect(Guid.NewGuid(),id,p.RowVersion,p with{Phone="74444444"},null,"Nuevo teléfono público");Saved(await online.SaveAsync(changed));p=(await Read(id))!;
        Check(p.DoNotContact&&(await online.ExclusionsAsync("+50374444444",new())).Total==1,"Editar ficha bloqueada no levanta exclusión e incorpora identificador nuevo");
        var reimport=Saved(await online.SaveAsync(New("Negocio con número antiguo","70000000")));Check((await Read(reimport))!.Suppressed,"Reingresar identificador anterior conserva supresión central");
        var exclude=new ExcludeOnlineContact(Guid.NewGuid(),"EMAIL","sincontacto@example.com","Petición explícita");var exclusionId=Saved(await online.SaveAsync(exclude));Saved(await online.SaveAsync(exclude));
        await c.ExecuteAsync("UPDATE com.ListaNoContactar SET Activo=0 WHERE IdNoContactar=@exclusionId;",new{exclusionId});
        Check(Saved(await online.SaveAsync(exclude with{OperationId=Guid.NewGuid()}))==exclusionId,"Alta restrictiva reactiva exclusión existente sin duplicar identificador");
        var suppressed=Saved(await online.SaveAsync(New("Negocio excluido",email:"SINCONTACTO@example.com")));Check((await Read(suppressed))!.Suppressed,"Alta nueva consulta exclusiones por canal sin otorgar consentimiento");
        var sr=new ExcludeOnlineContact(Guid.NewGuid(),"INSTAGRAM","https://www.instagram.com/negocio/","Solicitud social");Saved(await online.SaveAsync(sr));Check((await online.ExclusionsAsync("instagram.com/negocio",new())).Total==1,"Exclusión social se normaliza y valida también en repositorio");
        var customers=new CustomerService(new SqlCustomerRepository(factory),session);var type=(short)(await customers.CatalogsAsync()).Types[0].Id;
        var customer=(await customers.SaveAsync(new(new(){Name="Cliente comercial",TypeId=type,Phone="75555555",Email="cliente@example.com"},"Alta aislada"))).Value;
        Check((await online.SaveAsync(New("Prospecto ya cliente","+50375555555"))).ErrorCode=="DUPLICATE","Deduplica contra datos generales de Clientes");
        var cs=(await customers.ReadAsync(customer))!;await customers.SaveContactAsync(new(customer,cs.Customer.RowVersion,new(){Name="Contacto público",Email="contacto@example.com",Phone="76666666"},"Contacto aislado"));
        Check((await online.SaveAsync(New("Contacto ya registrado",email:"CONTACTO@example.com"))).ErrorCode=="DUPLICATE","Deduplica contra contactos secundarios del cliente");
        var existing=Saved(await online.SaveAsync(New("Prospecto anterior al cliente","77777777")));await c.ExecuteAsync("UPDATE cli.Clientes SET WhatsApp=N'+503 7777-7777' WHERE IdCliente=@customer;",new{customer});
        Check((await online.MatchesAsync(existing,new())).Items.Any(x=>x.Entity=="CLIENTE"&&x.Id==customer),"Ficha muestra coincidencia con cliente registrado posteriormente");
        var legacy=await c.ExecuteScalarAsync<long>("INSERT INTO com.Prospectos(Nombre,TelefonoPublico,EmailPublico,Departamento,Municipio) OUTPUT INSERTED.IdProspecto VALUES(N'Histórico',N'teléfono histórico ',N'correo heredado',N'Anterior',N'Anterior');");var lp=(await Read(legacy))!;
        Saved(await online.SaveAsync(new SaveOnlineProspect(Guid.NewGuid(),legacy,lp.RowVersion,lp with{Name="Histórico revisado"},null,"Conservar datos originales")));lp=(await Read(legacy))!;
        Check(lp.Phone=="teléfono histórico "&&lp.Email=="correo heredado"&&lp.Created is null,"Edición conserva contactos territorios y fecha histórica sin reconstruir");
        var broken=New("Rollback Online","78888888");await c.ExecuteAsync("CREATE TRIGGER sis.TR_TestOnlineAudit ON sis.Bitacora AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion LIKE N'ONLINE_%') THROW 50992,N'Fallo aislado de auditoría',1; END;");
        var failed=false;try{await online.SaveAsync(broken);}catch(SqlException ex)when(ex.Number==50992){failed=true;}finally{await c.ExecuteAsync("DROP TRIGGER sis.TR_TestOnlineAudit;");}
        Check(failed&&(await online.SearchAsync("Rollback Online",null,new())).Total==0,"Auditoría fallida revierte prospecto fuente y ledger");
        Check(await repo.FindOperationAsync(broken.OperationId,identity.Id,default)is null,"Rollback no deja recibo de alta confirmado");
        Check((await repo.ResolveOperationAsync(OperationIdentity.Describe(broken),true,identity.Id,identity.WorkstationId,default)).State==OperationResolutionState.RejectedDefinitively&&(await online.SaveAsync(broken)).ErrorCode=="OPERATION_REJECTED","Alta incierta ausente puede cerrarse e impide confirmación tardía");
        // Compra real para la conversión, con efectos comerciales anteriores al vínculo.
        var products=new ProductService(new SqlProductRepository(factory),session);var catalogs=(await products.GetCatalogsAsync()).Value!;var warehouse=await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes WHERE Activo=1 ORDER BY IdAlmacen;");
        var product=(int)(await products.SaveAsync(new(){Sku="ONLINE-P",Name="Producto sintético",CategoryId=catalogs.Categories[0].Id,UnitId=(short)catalogs.Units.First(x=>x.Name.Contains("Unidad")).Id,Price=10})).Value;
        Saved(await new InventorySetupService(repo,session).InitializeAsync(new(Guid.NewGuid(),product,warehouse,20,3,"Saldo aislado")));var cash=new CashService(repo,session);var box=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"ONLINE-CAJA","Caja Online",warehouse,true,"Alta aislada")));var shift=Saved(await cash.OpenAsync(new(Guid.NewGuid(),box,100,"Apertura aislada")));
        var sales=new SalesService(repo,session,gateway);var cart=new SaleCart(box,customer,[new(product,1)]);var quote=await sales.QuoteAsync(cart);var method=(await sales.PaymentMethodsAsync()).Single(x=>x.Code=="EFECTIVO").Id;
        var sale=Saved(await sales.ConfirmAsync(new(Guid.NewGuid(),cart,quote.Token,[new(method,quote.Total)],quote.Total,shift,"Compra confirmada aislada")));var sd=(await sales.ReadAsync(sale))!;
        Check((await online.SalesAsync(sd.Sale.Number,new())).Items.Single().Id==sale,"Conversión busca compra confirmada de cliente activo");
        p=(await Read(id))!;var conversion=new ConvertOnlineProspect(Guid.NewGuid(),id,p.RowVersion,sale,sd.Sale.RowVersion,"Identidad del negocio verificada");var stock=await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@product AND IdAlmacen=@warehouse;",new{product,warehouse});var expected=(await cash.ReadAsync(shift))!.Expected;
        Saved(await online.SaveAsync(conversion));Saved(await online.SaveAsync(conversion));p=(await Read(id))!;
        Check(p.State=="CONVERTIDO"&&p.CustomerId==customer&&p.ConversionSaleId==sale&&p.DoNotContact,"Vincular compra conserva trazabilidad fuente y exclusiones sin crear cliente");
        Check((await online.SourcesAsync(id,new())).Total==2&&await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cli.Clientes;")==1,"Conversión no duplica cliente ni borra fuentes");
        Check((await cash.ReadAsync(shift))!.Expected==expected&&await c.ExecuteScalarAsync<decimal>("SELECT StockActual FROM inv.ExistenciasProducto WHERE IdProducto=@product AND IdAlmacen=@warehouse;",new{product,warehouse})==stock,"Conversión no vuelve a vender ni modifica caja o inventario");
        Check(!(await online.SaveAsync(conversion with{OperationId=Guid.NewGuid(),ExpectedVersion=p.RowVersion})).IsSuccess,"Ficha convertida no se vuelve a vincular");
        Check(!(await online.SaveAsync(new ChangeOnlineState(Guid.NewGuid(),id,p.RowVersion,"REVISADO","Reabrir conversión"))).IsSuccess,"Estado interno no revierte una conversión");
        var otherData=(await Read(other))!;await c.ExecuteAsync("UPDATE ven.Ventas SET Estado=N'ANULADA' WHERE IdVenta=@sale;",new{sale});
        Check(!(await online.SaveAsync(new ConvertOnlineProspect(Guid.NewGuid(),other,otherData.RowVersion,sale,sd.Sale.RowVersion,"Venta anterior"))).IsSuccess,"Venta modificada no se vincula con versión anterior");
        var annulled=(await sales.ReadAsync(sale))!.Sale;Check(!(await online.SaveAsync(new ConvertOnlineProspect(Guid.NewGuid(),other,otherData.RowVersion,sale,annulled.RowVersion,"Venta anulada"))).IsSuccess,"Venta anulada no acredita conversión");
        Check((await Read(id))!.ConversionSaleId==sale,"Posventa posterior no borra trazabilidad histórica");
        checks+=await OnlinePlanningIntegration.RunAsync(factory,online,repo,session,root,database);
        var before=(await Read(id))!.RowVersion;await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));
        Check((await Read(id))!.RowVersion.SequenceEqual(before)&&(await Read(id))!.DoNotContact,"Reaplicar 025 conserva versiones conversiones y exclusiones");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.EXCLUSIONES');");
        Check((await online.SaveAsync(new ExcludeOnlineContact(Guid.NewGuid(),"EMAIL","nuevo@example.com","Permiso revocado"))).ErrorCode=="FORBIDDEN","SQL revalida permiso de exclusiones revocado pese a caché");
        await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));Check((await online.SaveAsync(new ExcludeOnlineContact(Guid.NewGuid(),"EMAIL","nuevo@example.com","Sigue revocado"))).ErrorCode=="FORBIDDEN","Reaplicar no concede permiso revocado");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'COMERCIAL.VER');");var denied=false;try{await online.ReadAsync(id);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"Lectura SQL exige permiso actual del módulo");
        Console.WriteLine($"{checks} comprobaciones de Online interno SQL correctas. Base aislada retenida: {database}");
    }
}
