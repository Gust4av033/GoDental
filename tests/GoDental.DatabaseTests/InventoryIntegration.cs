using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Repositories;
using GoDental.Infrastructure.Security;
using Microsoft.Data.SqlClient;

internal static class InventoryIntegration
{
    public static async Task RunAsync(IDbConnectionFactory factory)
    {
        int checks=0;
        void Check(bool ok,string label) { if(!ok) throw new InvalidOperationException(label); checks++; Console.WriteLine("OK "+label); }
        await using var c=await factory.OpenConnectionAsync();
        var gateway=new SqlIdentityGateway(factory,new Pbkdf2PasswordHasher());
        var identity=(await gateway.CreateAdministratorAsync("inventario_test","Administrador inventario",Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)),"INVENTORY-TEST")).Value!;
        var session=new UserSession(); session.Begin(identity);
        var products=new ProductService(new SqlProductRepository(factory),session);
        var repo=new SqlInventoryRepository(factory); var service=new InventoryService(repo,session);
        var catalogs=(await products.GetCatalogsAsync()).Value!;
        var warehouse=(await service.WarehousesAsync()).First().Id;
        var target=await c.ExecuteScalarAsync<int>("INSERT INTO inv.Almacenes(Codigo,Nombre) OUTPUT INSERTED.IdAlmacen VALUES(N'TEST_DEST',N'Destino prueba');");
        async Task<int> Product(string sku,bool lot=false)
        {
            var result=await products.SaveAsync(new(){Sku=sku,Name=sku,CategoryId=catalogs.Categories.First().Id,UnitId=(short)catalogs.Units.First().Id,TracksLot=lot,TracksExpiry=lot});
            Check(result.IsSuccess,"Alta producto "+sku); return result.Value;
        }
        async Task<byte[]> Version(int product,int w) => (await service.ReadAsync(product,w)).Stocks.Single(s=>s.WarehouseId==w).RowVersion;
        async Task<InventoryAdjustment> Request(int p,decimal quantity,decimal? cost=null,long? lot=null,int? destination=null) =>
            new(p,warehouse,quantity,cost,destination is null ? "AJUSTE" : "TRANSFERENCIA","Prueba física aislada",await Version(p,warehouse),lot,DestinationWarehouseId:destination);
        var simple=await Product("INV-SIMPLE");
        var entry=await Request(simple,10,2);
        Check((await service.AdjustAsync(entry)).IsSuccess,"Entrada crea saldo y Kardex");
        Check((await service.AdjustAsync(entry)).ErrorCode=="CONCURRENCY_CONFLICT","Repetir petición antigua no duplica entrada");
        Check((await service.AdjustAsync(await Request(simple,10,4))).IsSuccess,"Segunda entrada");
        var stock=(await service.ReadAsync(simple,warehouse)).Stocks.Single(s=>s.WarehouseId==warehouse);
        Check(stock.Quantity==20 && stock.AverageCost==3,"Promedio ponderado y saldo");
        Check((await service.AdjustAsync(await Request(simple,-2))).IsSuccess,"Salida a costo vigente");
        stock=(await service.ReadAsync(simple,warehouse)).Stocks.Single(s=>s.WarehouseId==warehouse);
        Check(stock.Quantity==18 && stock.AverageCost==3,"Salida conserva promedio");
        Check((await service.AdjustAsync(await Request(simple,-3,destination:target))).IsSuccess,"Transferencia atómica");
        var snapshot=await service.ReadAsync(simple,warehouse);
        Check(snapshot.Stocks.Single(s=>s.WarehouseId==warehouse).Quantity==15 && snapshot.Stocks.Single(s=>s.WarehouseId==target).Quantity==3 && snapshot.Stocks.Single(s=>s.WarehouseId==target).AverageCost==3,"Transferencia conserva cantidad y costo");
        var all=await service.MovementsAsync(new(simple,PageSize:100));
        Check(all.Total==5 && all.Items.All(m=>m.After==m.Before+m.Quantity),"Kardex cuadra y registra ambas piernas");
        Check((await service.MovementsAsync(new(simple,warehouse,PageSize:2))).Items.Count==2,"Kardex paginado");
        Check((await service.MovementsAsync(new(simple,warehouse,Type:"TRANSFERENCIA"))).Total==1,"Filtro de tipo y almacén");
        var categoryRepo=new SqlCategoryRepository(factory);
        var categoryId=(await products.CreateCategoryAsync("Administración aislada")).Value;
        var category=(await categoryRepo.ListAsync(identity.Id)).Single(x=>x.Id==categoryId);
        Check((await categoryRepo.SaveAsync(new(category.Id,"Renombrada",false,category.Name,category.Active),identity.Id,identity.WorkstationId)).IsSuccess,"Edita y desactiva categoría sin productos");
        Check((await categoryRepo.SaveAsync(new(category.Id,"Cambio obsoleto",true,category.Name,category.Active),identity.Id,identity.WorkstationId)).ErrorCode=="CONCURRENCY_CONFLICT","Rechaza categoría obsoleta");
        var tracked=await Product("INV-LOTE",true);
        var today=await c.ExecuteScalarAsync<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');");
        var trackedEntry=(await Request(tracked,5,8)) with {NewLotNumber="LOT-001",Expires=today.AddDays(20),Manufactured=today.AddDays(-30)};
        Check((await service.AdjustAsync(trackedEntry)).IsSuccess,"Entrada por lote con vencimiento");
        var lots=(await service.ReadAsync(tracked,warehouse)).Lots;
        var lot=lots.Single(); Check(lot.Quantity==5 && lot.Status=="Próximo a vencer","Alerta por vencimiento cercano");
        Check(!(await service.AdjustAsync((await Request(tracked,1,9)) with {NewLotNumber="LOT-001",Expires=today.AddDays(20)})).IsSuccess,"No duplica lote físico");
        Check(!(await service.AdjustAsync(await Request(tracked,-1))).IsSuccess,"Salida exige lote");
        Check((await service.SetLotStateAsync(new(lot.Id,true,false,"Retiro preventivo"))).IsSuccess,"Bloqueo auditado");
        snapshot=await service.ReadAsync(tracked,warehouse);
        Check(snapshot.Stocks.Single(s=>s.WarehouseId==warehouse).Quantity==5 && snapshot.Stocks.Single(s=>s.WarehouseId==warehouse).Sellable==0,"Bloqueo conserva físico y excluye vendible");
        Check(!(await service.AdjustAsync(await Request(tracked,1,8,lot.Id))).IsSuccess,"Bloqueado no admite entrada");
        Check((await service.AdjustAsync((await Request(tracked,-1,lot:lot.Id)) with {Type="DAÑO"})).IsSuccess,"Bloqueado admite baja por daño");
        Check(!(await service.AdjustAsync((await Request(tracked,-1,lot:lot.Id)) with {Type="VENCIMIENTO"})).IsSuccess,"No registra vencimiento de lote vigente");
        await c.ExecuteAsync("UPDATE inv.LotesProducto SET FechaVencimiento=@expired WHERE IdLote=@Id;",new {expired=today.AddDays(-1),lot.Id});
        Check((await service.AdjustAsync((await Request(tracked,-1,lot:lot.Id)) with {Type="VENCIMIENTO"})).IsSuccess,"Baja por lote vencido");
        var invalidAudit=await Request(simple,1,3);
        var before=(await service.ReadAsync(simple,warehouse)).Stocks.Single(s=>s.WarehouseId==warehouse);
        var movementsBefore=(await service.MovementsAsync(new(simple))).Total;
        bool failed=false;
        try { await repo.AdjustAsync(invalidAudit,identity.Id,int.MaxValue); } catch(SqlException) { failed=true; }
        Check(failed && (await service.ReadAsync(simple,warehouse)).Stocks.Single(s=>s.WarehouseId==warehouse).Quantity==before.Quantity && (await service.MovementsAsync(new(simple))).Total==movementsBefore,"Fallo auditoría revierte stock y Kardex");
        var last=await Product("INV-LAST");
        Check((await service.AdjustAsync(await Request(last,1,1))).IsSuccess,"Última unidad disponible");
        var outgoing=await Request(last,-1);
        var concurrent=await Task.WhenAll(service.AdjustAsync(outgoing),service.AdjustAsync(outgoing));
        Check(concurrent.Count(x=>x.IsSuccess)==1 && concurrent.Count(x=>x.ErrorCode=="CONCURRENCY_CONFLICT")==1,"Dos conexiones: sólo una consume última unidad");
        Check((await service.AdjustAsync(await Request(last,-1))).ErrorCode=="INSUFFICIENT_STOCK","Impide saldo negativo");
        await c.ExecuteAsync("UPDATE inv.ExistenciasProducto SET StockReservado=StockActual WHERE IdProducto=@simple AND IdAlmacen=@warehouse;",new {simple,warehouse});
        Check((await service.AdjustAsync(await Request(simple,-1))).ErrorCode=="INSUFFICIENT_STOCK","Protege stock reservado");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.COSTOS_VER');");
        Check((await service.ReadAsync(simple,warehouse)).Stocks.All(x=>x.AverageCost is null) && (await service.MovementsAsync(new(simple))).Items.All(x=>x.Cost is null),"Costos revocados no salen de SQL aunque sesión esté cacheada");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.AJUSTAR');");
        Check((await service.AdjustAsync(await Request(last,1,1))).ErrorCode=="FORBIDDEN","Revalida permiso de ajuste dentro de SQL");
        await c.ExecuteAsync("UPDATE seg.RolPermisos SET Concedido=0 WHERE IdPermiso=(SELECT IdPermiso FROM seg.Permisos WHERE Codigo=N'INVENTARIO.VER');");
        bool readDenied=false;try {await service.ReadAsync(simple,warehouse);}catch(SqlException ex) when(ex.Number==53001){readDenied=true;}
        Check(readDenied,"Revocación de lectura se aplica en SQL");
        Console.WriteLine($"{checks} comprobaciones de inventario correctas.");
    }
}
