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
internal static class CollectionIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory,string root,string database)
    {
        var checks=0;void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);checks++;Console.WriteLine("OK "+text);}
        long Saved(Result<MvpReceipt> r)=>r.IsSuccess?r.Value!.Id:throw new InvalidOperationException(r.ErrorCode+": "+r.Message);
        await using var c=await factory.OpenConnectionAsync();
        var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());
        var identity=(await gateway.CreateAdministratorAsync("collection_test","Administrador cobranza",Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),"COLLECTION-TEST")).Value!;
        var session=new UserSession();session.Begin(identity);var repo=new SqlMvpRepository(factory);var cash=new CashService(repo,session);
        var renderer=new CreditDocumentRenderer(Options.Create(new ReceiptPdfOptions()));var service=new CollectionService(repo,renderer,session);
        var customers=new CustomerService(new SqlCustomerRepository(factory),session);
        var type=(short)(await customers.CatalogsAsync()).Types[0].Id;
        var customer=(await customers.SaveAsync(new(new(){Name="Cliente de cobranza",TypeId=type},"Alta aislada"))).Value;
        var warehouse=await c.ExecuteScalarAsync<int>("SELECT TOP(1) IdAlmacen FROM inv.Almacenes ORDER BY IdAlmacen;");
        var box=(int)Saved(await cash.SaveBoxAsync(new(Guid.NewGuid(),0,"COL-CAJA","Caja cobranza",warehouse,true,"Alta aislada")));
        var today=await c.ExecuteScalarAsync<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        // Fixture financiera aislada: saldo financiado 100 sobre venta mixta total 120.
        var sale=await c.ExecuteScalarAsync<long>("INSERT INTO ven.Ventas(NumeroVenta,IdCliente,IdCaja,IdVendedor,IdEquipo,Estado,Total,SaldoPendiente) OUTPUT INSERTED.IdVenta VALUES(N'COL-VENTA',@customer,@box,@u,@s,N'CONFIRMADA',120,100);",new{customer,box,u=identity.Id,s=identity.WorkstationId});
        var accountId=await c.ExecuteScalarAsync<long>("INSERT INTO ven.CuentasPorCobrar(IdVenta,IdCliente,FechaEmision,FechaVencimiento,MontoOriginal,Saldo,Estado,ProximoAviso) OUTPUT INSERTED.IdCuenta VALUES(@sale,@customer,DATEADD(day,-10,@today),DATEADD(day,-1,@today),100,100,N'PENDIENTE',DATEADD(day,-1,@today));",new{sale,customer,today});
        async Task<CreditAccountData> Account()=>(await cash.AccountsAsync(false,new())).Items.Single(x=>x.Id==accountId);
        var account=await Account();Check(account.NoticeDue,"Cuenta expone aviso por atender con fecha local SQL");
        Check((await service.DueAccountsAsync(false,new())).Items.Single().Id==accountId,"Cola filtra avisos por atender en SQL");
        var request=new RegisterCollectionNotice(Guid.NewGuid(),accountId,account.RowVersion,"INTERNO","GESTION_INTERNA","Revisar saldo con el cliente");
        var notice=Saved(await service.RegisterAsync(request));Check(Saved(await service.RegisterAsync(request))==notice,"Reenvío de aviso conserva identificador y fecha");
        var notices=await service.NoticesAsync(accountId,new());Check(notices.Total==1&&notices.Items[0].Balance==100&&notices.Items[0].User==identity.DisplayName,"Congela saldo, usuario y observación del aviso");
        account=await Account();Check(account.NextNotice==today.AddDays(5)&&!account.NoticeDue,"Avanza próximo aviso cinco días y retira de pendientes");
        Check((await service.RegisterAsync(request with{OperationId=Guid.NewGuid()})).ErrorCode=="CONCURRENCY_CONFLICT","Otra estación no usa la versión anterior al aviso");
        Check((await service.RegisterAsync(request with{OperationId=Guid.NewGuid(),ExpectedVersion=account.RowVersion})).ErrorCode=="INVALID_STATE","No repite aviso antes de su fecha ni el mismo día");
        Check((await service.NoticesAsync(accountId,new(PageSize:1))).Items.Count==1&&(await service.NoticesAsync(accountId,new(2,1))).Items.Count==0,"Historial de avisos paginado sin duplicación");
        // Nuevo ciclo, prueba de dos estaciones sobre la misma versión.
        await c.ExecuteAsync("UPDATE ven.CuentasPorCobrar SET UltimoAviso=DATEADD(day,-6,@today),ProximoAviso=DATEADD(day,-1,@today) WHERE IdCuenta=@accountId;",new{today,accountId});
        // La fila anterior se conserva en otra fecha de prueba; nunca hay borrado de historia por el producto.
        await c.ExecuteAsync("UPDATE ven.AvisosCobranza SET FechaLocal=DATEADD(day,-6,@today) WHERE IdAviso=@notice;",new{today,notice});
        account=await Account();var concurrent=request with{OperationId=Guid.NewGuid(),ExpectedVersion=account.RowVersion};
        var race=await Task.WhenAll(service.RegisterAsync(concurrent),service.RegisterAsync(concurrent with{OperationId=Guid.NewGuid()}));
        Check(race.Count(x=>x.IsSuccess)==1&&race.Count(x=>x.ErrorCode=="CONCURRENCY_CONFLICT")==1,"Dos estaciones registran sólo un aviso por ciclo");
        var method=(await repo.PaymentMethodsAsync(identity.Id,default)).Single(x=>x.Code=="TRANSFERENCIA");
        account=await Account();var pay=new AccountPayment(Guid.NewGuid(),accountId,account.RowVersion,method.Id,30,"REF-COL-PRUEBA",null,"Abono de prueba");
        var payment=Saved(await cash.PayAsync(pay));Check(Saved(await cash.PayAsync(pay))==payment,"Abono recuperable conserva un solo recibo");
        Check((await Account()).NextNotice==today.AddDays(5),"Abono reprograma avisos usando día de El Salvador");
        var receipt=(await repo.PaymentReceiptAsync(payment,identity.Id,default))!;
        Check(receipt.BalanceBefore==100&&receipt.BalanceAfter==70&&receipt.Amount==30&&!receipt.HistoricalDetailsMissing,"Abono nuevo congela saldos antes/después y nombres");
        await c.ExecuteAsync("UPDATE cat.MetodosPago SET Nombre=@name WHERE IdMetodoPago=@id;",new{name=new string('M',80),id=method.Id});
        Check((await repo.PaymentReceiptAsync(payment,identity.Id,default))!.Method==method.Name,"Renombrar catálogo no cambia recibo original");
        account=await Account();var second=Saved(await cash.PayAsync(pay with{OperationId=Guid.NewGuid(),ExpectedVersion=account.RowVersion,Amount=10}));
        Check((await repo.PaymentReceiptAsync(second,identity.Id,default))!.Method.Length==80,"Snapshot admite longitud completa del catálogo de métodos");
        receipt=(await repo.PaymentReceiptAsync(payment,identity.Id,default))!;Check(receipt.BalanceAfter==70&&receipt.CurrentBalance==60,"Saldo actual se distingue del congelado en recibo anterior");
        var statement=(await service.StatementAsync(customer))!;
        Check(statement.Movements.Where(x=>x.Type=="CARGO").Sum(x=>x.Delta)==100&&statement.RecordedBalance==60&&statement.Difference==0,"Estado usa sólo monto financiado y cuadra con abonos");
        Check(statement.Movements.Last().RunningBalance==60,"Saldo acumulado sigue orden estable de movimientos");
        foreach(var layout in new[]{PrintLayout.Letter,new PrintLayout(80,250,3),new PrintLayout(58,250,3)})
        {Check((await service.PreviewStatementAsync(customer,layout)).Pdf.Length>1000,"Estado PDF completo en ancho "+layout.WidthMm);Check((await service.PreviewPaymentAsync(payment,layout)).Pages.Count>0,"Recibo PDF con vista previa en ancho "+layout.WidthMm);}
        // Registro anterior: no se fabrican snapshots ni saldos al imprimir.
        await c.ExecuteAsync("UPDATE ven.AbonosCliente SET ClienteNombreSnapshot=NULL,MetodoSnapshot=NULL,UsuarioSnapshot=NULL,SaldoAntes=NULL,SaldoDespues=NULL WHERE IdAbono=@second;",new{second});
        var legacy=(await repo.PaymentReceiptAsync(second,identity.Id,default))!;Check(legacy.HistoricalDetailsMissing&&legacy.BalanceBefore is null,"Recibo histórico indica datos no disponibles");
        var version=(await Account()).RowVersion;
        await DevelopmentDatabase.InstallAsync(database,Path.Combine(root,"database"));
        Check((await Account()).RowVersion.SequenceEqual(version)&&(await service.NoticesAsync(accountId,new())).Total==2,"Reaplicar 021 conserva avisos, saldos y versiones");
        // Error de auditoría revierte aviso + programación + ledger.
        await c.ExecuteAsync("UPDATE ven.CuentasPorCobrar SET UltimoAviso=DATEADD(day,-6,@today),ProximoAviso=DATEADD(day,-1,@today) WHERE IdCuenta=@accountId; UPDATE ven.AvisosCobranza SET FechaLocal=DATEADD(day,-5,@today) WHERE IdCuenta=@accountId AND FechaLocal=@today;",new{today,accountId});
        account=await Account();var failing=request with{OperationId=Guid.NewGuid(),ExpectedVersion=account.RowVersion};var failed=false;
        await c.ExecuteAsync("CREATE TRIGGER sis.TR_TestCollectionAudit ON sis.Bitacora AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE Accion=N'AVISO_COBRANZA') THROW 59999,N'Fallo de auditoría inyectado en base aislada',1; END;");
        try{await repo.RegisterNoticeAsync(failing,identity.Id,identity.WorkstationId,default);}catch(SqlException ex)when(ex.Number==59999){failed=true;}
        await c.ExecuteAsync("DROP TRIGGER sis.TR_TestCollectionAudit;");
        Check(failed&&(await Account()).RowVersion.SequenceEqual(account.RowVersion)&&await repo.FindOperationAsync(failing.OperationId,identity.Id,default)is null,"Fallo de FK/auditoría revierte aviso, cuenta y ledger");
        var ticket=OperationIdentity.Describe(failing);Check((await repo.ResolveOperationAsync(ticket,true,identity.Id,identity.WorkstationId,default)).State==OperationResolutionState.RejectedDefinitively,"Aviso incierto se puede cerrar sin efectos");
        Check((await service.RegisterAsync(failing)).ErrorCode=="OPERATION_REJECTED","Cierre durable impide registrar después la misma solicitud");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'CLIENTES.COBRANZA_GESTIONAR');");
        Check((await service.RegisterAsync(request with{OperationId=Guid.NewGuid(),ExpectedVersion=(await Account()).RowVersion})).ErrorCode=="FORBIDDEN","SQL revalida permiso de avisos pese a sesión cacheada");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'VENTAS.REIMPRIMIR');");
        var denied=false;try{await service.PreviewPaymentAsync(payment,PrintLayout.Letter);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"SQL revalida permiso de documentos antes de entregar PDF");
        Check((await service.StatementAsync(customer))!.RecordedBalance==60,"Lectura sin permiso de impresión conserva consulta permitida");
        Console.WriteLine($"{checks} comprobaciones de cobranza SQL correctas.");
    }
}
