using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    private const string ImportColumns = "i.IdImportacion Id,i.IdOrdenCompra OrderId,i.Courier,i.NumeroGuia Guide,i.Estado State,i.FechaEstimada ExpectedDate,i.FechaRecepcion ReceivedDate,i.CostosCerrados CostsClosed,i.TipoCambioReal Rate,ISNULL(i.TotalGastosUSD,0) TotalExpensesUsd,i.FechaCierre ClosedAt,i.RowVersion";
    private static Task<ImportationData?> Importation(Work w,long orderId) => w.Maybe<ImportationData>($"SELECT {ImportColumns} FROM cmp.Importaciones i WITH(UPDLOCK,HOLDLOCK) WHERE i.IdOrdenCompra=@orderId;",new {orderId});
    private static Task ImportNote(Work w,long id,string type,string description,int userId) => w.Execute("INSERT INTO cmp.EventosImportacion(IdImportacion,TipoEvento,Descripcion,Fuente,RegistradoPor) VALUES(@id,@type,@description,N'MANUAL',@userId);",new {id,type,description,userId});
    private static Task TouchImport(Work w,long id) => w.Execute("UPDATE cmp.Importaciones SET Observaciones=Observaciones WHERE IdImportacion=@id;",new {id});
    private static async Task NoReceipts(Work w,long orderId) => Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM cmp.RecepcionesCompra WITH(UPDLOCK,HOLDLOCK) WHERE IdOrdenCompra=@orderId AND Estado<>N'ANULADA') OR EXISTS(SELECT 1 FROM cmp.OrdenCompraDetalle WHERE IdOrdenCompra=@orderId AND CantidadRecibida>0) THEN 1 ELSE 0 END);",new {orderId}),"INVALID_STATE","La orden ya tiene recepciones vigentes o unidades recibidas. Revisa su historial antes de crear o cerrar la importación.");
    private static async Task<ImportationData> LockImport(Work w,long orderId,long id,byte[] version)
    {
        var i=await Importation(w,orderId); Version(i?.RowVersion,version);
        Require(i!.Id==id,"INVALID_STATE","La importación no pertenece a esta orden."); return i;
    }
    public Task<ImportationSnapshot?> ReadImportAsync(long orderId,MvpFilter events,int userId,CancellationToken ct) => Read<ImportationSnapshot?>(userId,["COMPRAS.VER"],async w =>
    {
        // La lectura toma primero OC para mantener el mismo orden de bloqueos que las escrituras.
        if(await Purchase(w,orderId,true) is null) return null;
        var i=await Importation(w,orderId); if(i is null)return null;
        var expenses=await w.List<ImportExpenseData>("SELECT g.IdGasto Id,g.TipoGasto Type,ISNULL(g.Descripcion,N'') Description,g.Monto Amount,m.CodigoISO Currency,ISNULL(g.TipoCambio,0) Rate,g.MontoUSD AmountUsd,g.FechaGasto Date,g.Anulado Voided FROM cmp.GastosImportacion g JOIN cat.Monedas m ON m.IdMoneda=g.IdMoneda WHERE g.IdImportacion=@id ORDER BY g.IdGasto DESC;",new{id=i.Id});
        var page=await Page<ImportEventData>(w,"cmp.EventosImportacion e LEFT JOIN seg.Usuarios u ON u.IdUsuario=e.RegistradoPor","e.IdEvento Id,e.FechaEvento Date,e.TipoEvento Type,e.Descripcion Description,ISNULL(u.NombreCompleto,N'Histórico') [User]","e.IdImportacion=@id","e.FechaEvento DESC,e.IdEvento DESC",new{id=i.Id,Offset=checked((events.Page-1)*events.PageSize),events.PageSize});
        return new(i,expenses,page.Items,page.Total,events.Page);
    },ct);
    public Task<Result<MvpReceipt>> WriteImportAsync(object request,int userId,int? workstationId,CancellationToken ct)
    {
        if(ImportValidator.Validate(request) is {} error)return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error));
        var r=ImportValidator.Freeze(request);
        return Write(ImportValidator.Id(r),ImportValidator.Type(r),r,userId,workstationId,["COMPRAS.VER","COMPRAS.GESTIONAR"],async w =>
        {
            await Station(w,workstationId);
            if(r is SaveImportation create)
            {
                var createdOrder=await LockPurchase(w,create.OrderId,create.ExpectedOrderVersion);
                Require(createdOrder.Date<=await w.Scalar<DateTime>("SELECT CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time');"),"VALIDATION","La fecha de la orden es futura. Revisa la compra antes de registrar su importación.");
                Require(PurchaseRules.CanReceive(createdOrder.State),"INVALID_STATE","Confirma la orden antes de crear la importación.");
                await NoReceipts(w,createdOrder.Id); Require(await Importation(w,createdOrder.Id) is null,"DUPLICATE","La orden ya tiene una importación.");
                var id=await w.Scalar<long>("INSERT INTO cmp.Importaciones(IdOrdenCompra,Courier,NumeroGuia,FechaPedido,FechaEstimada,Observaciones) OUTPUT INSERTED.IdImportacion VALUES(@OrderId,@Courier,@Guide,@date,@ExpectedDate,@Reason);",new{create.OrderId,create.Courier,create.Guide,date=createdOrder.Date,create.ExpectedDate,create.Reason});
                await ImportNote(w,id,"CREADA",create.Reason,userId);await TouchPurchase(w,createdOrder.Id);return id;
            }
            var orderId=r switch {ImportEvent x=>x.OrderId,ImportExpense x=>x.OrderId,VoidImportExpense x=>x.OrderId,CloseImportCosts x=>x.OrderId,PrepareImportReceipt x=>x.Receipt.OrderId,_=>0};
            var importId=r switch {ImportEvent x=>x.ImportId,ImportExpense x=>x.ImportId,VoidImportExpense x=>x.ImportId,CloseImportCosts x=>x.ImportId,PrepareImportReceipt x=>x.ImportId,_=>0};
            var version=r switch {ImportEvent x=>x.ExpectedVersion,ImportExpense x=>x.ExpectedVersion,VoidImportExpense x=>x.ExpectedVersion,CloseImportCosts x=>x.ExpectedVersion,PrepareImportReceipt x=>x.ExpectedImportVersion,_=>[]};
            var order=await Purchase(w,orderId,true);Require(order is not null && order.State!="CANCELADA","INVALID_STATE","La orden no está disponible.");
            var import=await LockImport(w,orderId,importId,version);
            switch(r)
            {
                case ImportEvent e:
                    Require(ImportRules.CanMove(import.State,e.State),"INVALID_STATE","El estado logístico no permite esa transición. La recepción física determina los estados recibidos.");
                    await w.Execute("UPDATE cmp.Importaciones SET Estado=@State,Courier=@Courier,NumeroGuia=@Guide,FechaEstimada=@ExpectedDate,FechaDespacho=CASE WHEN @State=N'DESPACHADA' AND FechaDespacho IS NULL THEN CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time') ELSE FechaDespacho END WHERE IdImportacion=@ImportId;",e);
                    await ImportNote(w,import.Id,e.State,$"{e.Reason} | Guía: {e.Guide ?? "sin guía"}",userId);break;
                case ImportExpense e:
                    Require(!import.CostsClosed,"INVALID_STATE","Los costos están cerrados: no se agregan gastos posteriores.");
                    Require(await w.Scalar<int>("SELECT COUNT(*) FROM cmp.GastosImportacion WHERE IdImportacion=@id;",new{id=import.Id})<500,"VALIDATION","Usa hasta 500 registros de gasto por importación.");
                    await CurrencyRate(w,e.CurrencyId,e.Rate);var usd=PurchaseRules.Money(e.Amount*e.Rate);
                    Require(MvpValidator.Money(usd,true),"VALIDATION","El gasto convertido supera el rango o redondea a cero.");
                    var expense=await w.Scalar<long>("INSERT INTO cmp.GastosImportacion(IdImportacion,TipoGasto,Descripcion,Monto,IdMoneda,TipoCambio,MontoUSD,FechaGasto) OUTPUT INSERTED.IdGasto VALUES(@ImportId,@Type,@Description,@Amount,@CurrencyId,@Rate,@usd,@Date);",new{e.ImportId,e.Type,e.Description,e.Amount,e.CurrencyId,e.Rate,usd,e.Date});
                    await ImportNote(w,import.Id,"GASTO",$"Gasto #{expense} · {e.Type} · USD {usd:0.00}. {e.Reason}",userId);break;
                case VoidImportExpense e:
                    Require(!import.CostsClosed,"INVALID_STATE","Los costos están cerrados: no se anulan gastos.");
                    Require(await w.Execute("UPDATE cmp.GastosImportacion SET Anulado=1 WHERE IdImportacion=@ImportId AND IdGasto=@ExpenseId AND Anulado=0;",e)==1,"INVALID_STATE","El gasto no está disponible o ya fue anulado.");
                    await ImportNote(w,import.Id,"ANULAR_GASTO",$"Gasto #{e.ExpenseId}: {e.Reason}",userId);break;
                case CloseImportCosts e:
                    Require(!import.CostsClosed,"INVALID_STATE","Los costos ya fueron cerrados.");await NoReceipts(w,orderId);await CurrencyRate(w,order!.CurrencyId,e.Rate);
                    var lines=await PurchaseLines(w,orderId);
                    var total=await w.Scalar<decimal>("SELECT ISNULL(SUM(MontoUSD),0) FROM cmp.GastosImportacion WITH(UPDLOCK,HOLDLOCK) WHERE IdImportacion=@id AND Anulado=0;",new{id=import.Id});
                    Require(MvpValidator.Money(total),"VALIDATION","El total de gastos supera el rango.");
                    Require(lines.Count>0 && (total==0 || lines.Sum(l=>l.Total)>0),"VALIDATION","La compra debe tener valor neto para distribuir gastos.");
                    var budgets=ImportRules.Allocate(total,lines.Select(l=>(l.Id,l.Total)).ToArray());
                    foreach(var line in lines)
                    {
                        var cost=PurchaseRules.PurchaseCostUsd(line.UnitCost,line.Discount,line.Quantity,e.Rate);
                        Require(PurchaseValidator.Cost(cost) && PurchaseValidator.Cost(cost+PurchaseRules.Cost(budgets[line.Id]/line.Quantity)),"VALIDATION","El costo puesto en bodega supera el rango.");
                        await w.Execute("UPDATE cmp.OrdenCompraDetalle SET CostoImportacionUSD=@cost,GastoImportacionUSD=@budget WHERE IdDetalle=@id;",new{cost,budget=budgets[line.Id],id=line.Id});
                    }
                    await w.Execute("UPDATE cmp.Importaciones SET CostosCerrados=1,TipoCambioReal=@Rate,TotalGastosUSD=@total,FechaCierre=SYSDATETIMEOFFSET(),CerradoPor=@userId WHERE IdImportacion=@ImportId;",new{e.Rate,total,userId,e.ImportId});
                    await ImportNote(w,import.Id,"CIERRE_COSTOS",$"Cambio real {e.Rate}. Gastos USD {total:0.00}. {e.Reason}",userId);break;
                case PrepareImportReceipt e:
                    Version(order!.RowVersion,e.Receipt.ExpectedOrderVersion);
                    Require(import.CostsClosed && import.Rate==e.Receipt.ExchangeRate && e.Receipt.CostsComplete && e.Receipt.Lines.All(l=>l.ExtraUnitCostUsd==0),"INVALID_STATE","Cierra los costos y usa el cambio congelado, sin gastos unitarios manuales.");
                    return await PrepareReceiptCore(w,e.Receipt,order,userId,import);
            }
            await TouchImport(w,import.Id);await TouchPurchase(w,orderId);return import.Id;
        },ct);
    }
    private static async Task<IReadOnlyDictionary<int,decimal>> ReceiptExpenses(Work w,IReadOnlyList<PurchaseOrderLineData> order,IReadOnlyList<ReceivePurchaseLine> incoming)
    {
        var result=new Dictionary<int,decimal>();
        foreach(var group in incoming.Select((line,index)=>(line,index)).GroupBy(x=>x.line.OrderLineId))
        {
            var source=order.Single(x=>x.Id==group.Key);
            Require(source.ImportExpenseUsd is not null && source.ImportPurchaseCostUsd is not null,"INVALID_STATE","La línea no tiene costos de importación cerrados.");
            var spent=await w.Scalar<decimal>("SELECT ISNULL(SUM(d.GastoImportacionTotalUSD),0) FROM cmp.RecepcionCompraDetalle d JOIN cmp.RecepcionesCompra r ON r.IdRecepcion=d.IdRecepcion WHERE d.IdOrdenCompraDetalle=@id AND r.Estado=N'APLICADA';",new{id=source.Id});
            var total=ImportRules.PartialExpense(source.ImportExpenseUsd!.Value,source.Quantity,source.Received,group.Sum(x=>x.line.Quantity),spent);
            var slices=ImportRules.Allocate(total,group.Select(x=>((long)x.index,x.line.Quantity)).ToArray());
            foreach(var x in slices)result[(int)x.Key]=x.Value;
        }
        return result;
    }
}
