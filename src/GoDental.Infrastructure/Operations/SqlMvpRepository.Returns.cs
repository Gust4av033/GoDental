using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;

namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    public Task<ReturnEvidenceLocation> ReadEvidenceLocationAsync(int userId,CancellationToken ct)=>Read(userId,["VENTAS.VER","AJUSTES.EDITAR"],w=>w.One<ReturnEvidenceLocation>("SELECT Valor Root,RowVersion FROM sis.Configuracion WHERE Clave=N'Archivos.RaizEvidencias';"),ct);
    public Task<Result<MvpReceipt>> ConfigureEvidenceAsync(ConfigureReturnEvidence r,int userId,int? workstationId,CancellationToken ct)=>Write(r.OperationId,"CARPETA_EVIDENCIAS",r,userId,workstationId,["VENTAS.VER","AJUSTES.EDITAR"],async w=>
    {
        if(SaleReturnValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
        await Station(w,workstationId);await RequireReturnAdministrator(w,userId);
        var root=GoDental.Infrastructure.Data.FileReturnEvidenceStore.ValidateRoot(r.Root);
        var previous=await w.One<ReturnEvidenceLocation>("SELECT Valor Root,RowVersion FROM sis.Configuracion WITH(XLOCK,HOLDLOCK) WHERE Clave=N'Archivos.RaizEvidencias';");Version(previous.RowVersion,r.ExpectedVersion);
        Require(previous.Root==root || !await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM ven.DevolucionEvidencias WITH(HOLDLOCK)) THEN 1 ELSE 0 END);"),"INVALID_STATE","La carpeta ya tiene fotos registradas. Su traslado requiere mantenimiento y copia verificada de evidencias antes de cambiar la ruta.");
        await w.Execute("UPDATE sis.Configuracion SET Valor=@root,ModificadoPor=@userId,FechaModificacion=SYSDATETIMEOFFSET() WHERE Clave=N'Archivos.RaizEvidencias';",new{root,userId});return 1L;
    },ct);
    private static async Task RequireReturnAdministrator(Work w,int administratorId)
    {
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.UsuarioRoles u JOIN seg.Roles r ON r.IdRol=u.IdRol AND r.Activo=1 WHERE u.IdUsuario=@administratorId AND r.Nombre=N'Administrador') THEN 1 ELSE 0 END);",new{administratorId}),"FORBIDDEN","La operación requiere una cuenta administradora vigente.");
    }
    private const string ReturnColumns="d.IdDevolucion Id,d.NumeroDevolucion Number,d.IdVenta SaleId,d.FechaSolicitud Requested,u.NombreCompleto Requester,a.NombreCompleto Administrator,d.Estado State,d.EsAnulacion CancelSale,ISNULL(d.Comprobante,N'') Proof,d.Motivo Reason,d.MotivoResolucion Resolution,d.MontoReintegro Amount,d.ReduccionDeuda DebtReduction,d.ReintegroEfectivo CashRefund,d.IdTurno ShiftId,d.FechaAplicacion Applied,d.RowVersion";
    private const string ReturnFrom="ven.DevolucionesVenta d JOIN seg.Usuarios u ON u.IdUsuario=d.SolicitadaPor LEFT JOIN seg.Usuarios a ON a.IdUsuario=d.AutorizadaPor";
    private static Task<IReadOnlyList<ReturnableSaleLine>> ReturnableLines(Work w,long saleId)=>w.List<ReturnableSaleLine>("""
        SELECT v.IdDetalle Id,v.SkuSnapshot Sku,v.DescripcionSnapshot Name,v.Cantidad Sold,v.TotalLinea Total,
          ISNULL((SELECT SUM(d.Cantidad) FROM ven.DevolucionDetalle d JOIN ven.DevolucionesVenta r ON r.IdDevolucion=d.IdDevolucion AND r.Estado=N'APLICADA' WHERE d.IdVentaDetalle=v.IdDetalle),0) Returned,
          ISNULL((SELECT SUM(d.MontoReintegrado) FROM ven.DevolucionDetalle d JOIN ven.DevolucionesVenta r ON r.IdDevolucion=d.IdDevolucion AND r.Estado=N'APLICADA' WHERE d.IdVentaDetalle=v.IdDetalle),0) Refunded
        FROM ven.VentaDetalle v WHERE v.IdVenta=@saleId ORDER BY v.IdDetalle;
        """,new{saleId});
    private static async Task<int> ReturnWindow(Work w)
    {
        var hours=await w.Scalar<int?>("SELECT TRY_CONVERT(int,Valor) FROM sis.Configuracion WITH(HOLDLOCK) WHERE Clave=N'Devolucion.PlazoHoras';");
        Require(hours is >0,"INVALID_STATE","Revisa el parámetro Devolucion.PlazoHoras.");return hours!.Value;
    }
    // El pedido se bloquea antes de venta, igual que vincular/despachar/confirmar desde pedido.
    private static Task<OrderData?> ReturnOrder(Work w,long saleId)=>w.Maybe<OrderData>("SELECT IdPedido Id,IdVenta SaleId,Estado State,RowVersion FROM ven.PedidosMvp WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@saleId;",new{saleId});
    private static async Task<SaleData> ReturnSale(Work w,long saleId)
    {
        var sale=await w.Maybe<SaleData>($"SELECT {SaleColumns} FROM ven.Ventas WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@saleId;",new{saleId});
        Require(sale is not null,"INVALID_STATE","La venta no existe.");return sale!;
    }
    public Task<SaleReturnsSnapshot> ReadReturnsAsync(long saleId,MvpFilter filter,int userId,CancellationToken ct)=>Read(userId,["VENTAS.VER"],async w=>
    {
        await ReturnOrder(w,saleId);await ReturnSale(w,saleId);
        var lines=await ReturnableLines(w,saleId);
        var page=await Page<SaleReturnData>(w,ReturnFrom,ReturnColumns,"d.IdVenta=@saleId","d.IdDevolucion DESC",new{saleId,Offset=checked((filter.Page-1)*filter.PageSize),filter.PageSize});
        return new SaleReturnsSnapshot(lines,page.Items,page.Total,filter.Page,await ReturnWindow(w),await w.Scalar<DateTimeOffset>("SELECT CONVERT(datetimeoffset(0),SYSDATETIMEOFFSET());"));
    },ct);
    private static async Task<IReadOnlyList<SaleReturnLineData>> ReturnDetails(Work w,long id)
    {
        var lines=await w.List<SaleReturnLineData>("SELECT d.IdDetalle Id,d.IdVentaDetalle SaleLineId,v.SkuSnapshot Sku,v.DescripcionSnapshot Name,d.Cantidad Quantity,d.Condicion Condition,d.ReingresaInventario Restock,d.MotivoNoReingreso NoRestockReason,d.MontoReintegrado Amount FROM ven.DevolucionDetalle d JOIN ven.VentaDetalle v ON v.IdDetalle=d.IdVentaDetalle WHERE d.IdDevolucion=@id ORDER BY d.IdDetalle;",new{id});
        foreach(var line in lines) line.Evidence=(await w.List<ReturnEvidence>("SELECT NombreArchivo Name,MimeType,RutaRelativa RelativePath,HashSha256 Sha256 FROM ven.DevolucionEvidencias WHERE IdDevolucionDetalle=@Id ORDER BY IdEvidencia;",line)).ToList();
        return lines;
    }
    private static async Task<SaleReturnSnapshot> ReturnSnapshot(Work w,SaleData sale,SaleReturnData request,OrderData? order)
    {
        var lines=await ReturnDetails(w,request.Id);var available=await ReturnableLines(w,sale.Id);
        if(request.State!="SOLICITADA")return new(request,lines,request.Amount,request.DebtReduction,request.CashRefund,"",null);
        var account=await w.Maybe<CreditAccountData>("SELECT IdCuenta Id,Saldo Balance,Estado State,RowVersion FROM ven.CuentasPorCobrar WITH(UPDLOCK,HOLDLOCK) WHERE IdVenta=@Id;",sale);
        decimal amount=0;string? unavailable=null;
        try
        {
            Require(sale.State is "CONFIRMADA" or "DEVUELTA_PARCIAL","INVALID_STATE","La venta ya fue anulada o devuelta completamente.");
            Require(!string.IsNullOrWhiteSpace(request.Proof)&&lines.Count>0&&lines.Select(l=>l.SaleLineId).Distinct().Count()==lines.Count,"INVALID_STATE","La solicitud histórica no tiene comprobante/detalle completo; recházala y presenta una nueva.");
            Require(lines.All(l=>l.Quantity>0&&(!l.Restock||l.Condition=="SELLADO")&&(l.Restock||!string.IsNullOrWhiteSpace(l.NoRestockReason))&&(l.Condition!="ABIERTO_DEFECTUOSO"||l.Evidence.Count>0)),"INVALID_STATE","Revisa condición, evidencia y destino de todos los productos.");
            if(request.CancelSale) ValidateCancellation(available,lines);
            foreach(var line in lines)
            {
                var original=available.SingleOrDefault(x=>x.Id==line.SaleLineId);
                Require(original is not null,"INVALID_STATE","Una línea no pertenece a la venta.");
                amount+=ReturnRules.Amount(original!.Sold,original.Total,original.Returned,original.Refunded,line.Quantity);
            }
            if(request.CancelSale) amount+=sale.DeliveryCharge;
            Require((account?.Balance??0)==sale.Pending && account?.State!="CASTIGADA","INVALID_STATE","La cuenta requiere revisión antes de reintegrar.");
            Require(!await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM fis.DocumentosFiscales WITH(HOLDLOCK) WHERE IdVenta=@Id AND Estado NOT IN(N'PENDIENTE',N'ANULADO')) THEN 1 ELSE 0 END);",sale),"INVALID_STATE","La venta tiene un documento fiscal emitido: requiere su flujo de reversión fiscal.");
        }
        catch(RuleFailure ex){unavailable=ex.Message;}
        catch(ArgumentException ex){unavailable=ex.Message;}
        var (debt,cash)=ReturnRules.Settlement(amount,sale.Pending);
        var token=HashToken(new{sale.RowVersion,RequestVersion=request.RowVersion,AccountVersion=account?.RowVersion,OrderVersion=order?.RowVersion,amount,debt,cash,available});
        return new(request,lines,amount,debt,cash,token,unavailable);
    }
    private static void ValidateCancellation(IReadOnlyList<ReturnableSaleLine> available,IReadOnlyList<SaleReturnLineData> lines)
    {
        Require(available.All(x=>x.Returned==0)&&available.Count==lines.Count&&available.All(x=>lines.Any(l=>l.SaleLineId==x.Id&&l.Quantity==x.Sold&&l.Restock&&l.Condition=="SELLADO")),"INVALID_STATE","La anulación requiere toda la mercadería sellada, sin devoluciones anteriores.");
    }
    public Task<SaleReturnSnapshot?> ReadReturnAsync(long saleId,long returnId,int userId,CancellationToken ct)=>Read(userId,["VENTAS.VER"],async w=>
    {
        var order=await ReturnOrder(w,saleId);var sale=await ReturnSale(w,saleId);
        var request=await w.Maybe<SaleReturnData>($"SELECT {ReturnColumns} FROM {ReturnFrom} WHERE d.IdDevolucion=@returnId AND d.IdVenta=@saleId;",new{returnId,saleId});
        return request is null?null:await ReturnSnapshot(w,sale,request,order);
    },ct);
    public Task<Result<MvpReceipt>> RequestReturnAsync(RequestSaleReturn r,int userId,int? workstationId,CancellationToken ct)=>Write(r.OperationId,"SOLICITAR_DEVOLUCION",r,userId,workstationId,["VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR"],async w=>
    {
        if(SaleReturnValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
        await Station(w,workstationId);
        if(r.CancelSale)await w.Authorize(userId,"VENTAS.ANULAR");
        var order=await ReturnOrder(w,r.SaleId);var sale=await ReturnSale(w,r.SaleId);Version(sale.RowVersion,r.ExpectedSaleVersion);
        Require(sale.State is "CONFIRMADA" or "DEVUELTA_PARCIAL","INVALID_STATE","La venta ya fue anulada o devuelta completamente.");
        var now=await w.Scalar<DateTimeOffset>("SELECT CONVERT(datetimeoffset(0),SYSDATETIMEOFFSET());");
        Require(ReturnRules.WithinWindow(sale.Date,now,await ReturnWindow(w)),"INVALID_STATE","La compra está fuera del plazo configurado para solicitar devolución/anulación.");
        // La raíz no puede cambiar entre verificación del archivo y registro de sus referencias.
        var evidenceRoot=await w.Scalar<string>("SELECT Valor FROM sis.Configuracion WITH(HOLDLOCK) WHERE Clave=N'Archivos.RaizEvidencias';");
        var available=await ReturnableLines(w,r.SaleId);var calculated=new List<SaleReturnLineData>();
        foreach(var item in r.Items)
        {
            var original=available.SingleOrDefault(x=>x.Id==item.SaleLineId);
            Require(original is not null,"VALIDATION","Una línea no pertenece a la venta.");
            Require(!await w.Scalar<bool>("SELECT ControlaSerie FROM inv.Productos p JOIN ven.VentaDetalle d ON d.IdProducto=p.IdProducto WHERE d.IdDetalle=@SaleLineId;",item),"INVALID_STATE","La devolución de series requiere su complemento específico.");
            Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN u.PermiteDecimales=1 OR FLOOR(@Quantity)=@Quantity THEN 1 ELSE 0 END) FROM ven.VentaDetalle d JOIN inv.Productos p ON p.IdProducto=d.IdProducto JOIN cat.UnidadesMedida u ON u.IdUnidadMedida=p.IdUnidadMedida WHERE d.IdDetalle=@SaleLineId;",item),"VALIDATION","La unidad del producto no admite devoluciones fraccionarias.");
            var amount=ReturnRules.Amount(original!.Sold,original.Total,original.Returned,original.Refunded,item.Quantity);
            foreach(var e in item.Evidence)Require(returnEvidence is not null && await returnEvidence.VerifyAsync(e,userId,ct,evidenceRoot),"VALIDATION","No se pudo verificar una foto en el almacenamiento central. Adjunta nuevamente la evidencia.");
            calculated.Add(new(){SaleLineId=item.SaleLineId,Quantity=item.Quantity,Condition=item.Condition,Restock=item.Restock,Amount=amount});
        }
        if(r.CancelSale)ValidateCancellation(available,calculated);
        var total=calculated.Sum(x=>x.Amount)+(r.CancelSale?sale.DeliveryCharge:0);
        var number=await Number(w,"DEVOLUCION");
        var id=await w.Scalar<long>("INSERT INTO ven.DevolucionesVenta(NumeroDevolucion,IdVenta,FechaSolicitud,SolicitadaPor,Motivo,FormaReintegro,MontoReintegro,Estado,EsAnulacion,Comprobante,IdEquipo) OUTPUT INSERTED.IdDevolucion VALUES(@number,@SaleId,@now,@userId,@Reason,N'EFECTIVO',@total,N'SOLICITADA',@CancelSale,@Proof,@workstationId);",new{number,r.SaleId,now,userId,r.Reason,total,r.CancelSale,r.Proof,workstationId});
        foreach(var item in r.Items)
        {
            var amount=calculated.Single(x=>x.SaleLineId==item.SaleLineId).Amount;
            var detail=await w.Scalar<long>("INSERT INTO ven.DevolucionDetalle(IdDevolucion,IdVentaDetalle,Cantidad,Condicion,ReingresaInventario,MotivoNoReingreso,MontoReintegrado) OUTPUT INSERTED.IdDetalle VALUES(@id,@SaleLineId,@Quantity,@Condition,@Restock,@NoRestockReason,@amount);",new{id,item.SaleLineId,item.Quantity,item.Condition,item.Restock,item.NoRestockReason,amount});
            foreach(var e in item.Evidence)await w.Execute("INSERT INTO ven.DevolucionEvidencias(IdDevolucionDetalle,NombreArchivo,MimeType,RutaRelativa,HashSha256,SubidaPor) VALUES(@detail,@Name,@MimeType,@RelativePath,@Sha256,@userId);",new{detail,e.Name,e.MimeType,e.RelativePath,e.Sha256,userId});
        }
        await w.Execute("UPDATE ven.Ventas SET SaldoPendiente=SaldoPendiente WHERE IdVenta=@SaleId;",r);
        return id;
    },ct);
    public Task<Result<MvpReceipt>> ResolveReturnAsync(ResolveSaleReturn r,int administratorId,int userId,int? workstationId,CancellationToken ct)=>Write(r.OperationId,r.Apply?"APLICAR_DEVOLUCION":"RECHAZAR_DEVOLUCION",new{r,administratorId},userId,workstationId,["VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR"],async w=>
    {
        if(SaleReturnValidator.Validate(r) is {} error)throw new RuleFailure("VALIDATION",error);
        await Station(w,workstationId);await w.Authorize(administratorId,"VENTAS.DEVOLUCION_AUTORIZAR");
        Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM seg.UsuarioRoles u JOIN seg.Roles r ON r.IdRol=u.IdRol AND r.Activo=1 WHERE u.IdUsuario=@administratorId AND r.Nombre=N'Administrador') THEN 1 ELSE 0 END);",new{administratorId}),"FORBIDDEN","La autorización requiere una cuenta administradora vigente.");
        var order=await ReturnOrder(w,r.SaleId);
        CashShiftData? shift=r.Apply&&r.ShiftId is {} shiftId?await LockShift(w,shiftId):null;
        var sale=await ReturnSale(w,r.SaleId);
        var request=await w.Maybe<SaleReturnData>($"SELECT {ReturnColumns} FROM {ReturnFrom} WHERE d.IdDevolucion=@ReturnId AND d.IdVenta=@SaleId;",r);
        Require(request is not null&&request.State=="SOLICITADA","INVALID_STATE","La solicitud no está pendiente.");Version(request!.RowVersion,r.ExpectedVersion);
        if(request.CancelSale)await w.Authorize(administratorId,"VENTAS.ANULAR");
        SaleReturnSnapshot? snapshot=null;
        if(r.Apply)
        {
            snapshot=await ReturnSnapshot(w,sale,request,order);
            Require(snapshot.UnavailableReason is null,"INVALID_STATE",snapshot.UnavailableReason??"");
            Require(snapshot.QuoteToken==r.QuoteToken,"CONCURRENCY_CONFLICT","Cambió la venta, deuda o una devolución. Actualiza y revisa el reintegro.");
            Require(snapshot.CashRefund==0 || shift is not null&&shift.CashBoxId==sale.CashBoxId&&shift.Expected>=snapshot.CashRefund,"INVALID_STATE","El reintegro requiere un turno abierto de la caja de la venta con efectivo suficiente.");
            var evidenceRoot=await w.Scalar<string>("SELECT Valor FROM sis.Configuracion WITH(HOLDLOCK) WHERE Clave=N'Archivos.RaizEvidencias';");
            var requester=await w.Scalar<int>("SELECT SolicitadaPor FROM ven.DevolucionesVenta WHERE IdDevolucion=@ReturnId;",r);
            foreach(var line in snapshot.Lines)
            {
                foreach(var e in line.Evidence)Require(returnEvidence is not null&&await returnEvidence.VerifyAsync(e,requester,ct,evidenceRoot),"INVALID_STATE","Una foto no está disponible o cambió. No se aplicó la devolución.");
                if(line.Restock)await RestockReturn(w,line,sale.CashBoxId,r.ReturnId,userId);
                var original=(await ReturnableLines(w,r.SaleId)).Single(x=>x.Id==line.SaleLineId);
                var amount=ReturnRules.Amount(original.Sold,original.Total,original.Returned,original.Refunded,line.Quantity);
                await w.Execute("UPDATE ven.DevolucionDetalle SET MontoReintegrado=@amount WHERE IdDetalle=@Id;",new{amount,line.Id});
            }
            if(snapshot.DebtReduction>0)await w.Execute("UPDATE ven.CuentasPorCobrar SET Saldo=Saldo-@debt,Estado=CASE WHEN Saldo-@debt=0 THEN N'PAGADA' ELSE N'PARCIAL' END,ProximoAviso=CASE WHEN Saldo-@debt=0 THEN NULL ELSE ProximoAviso END WHERE IdVenta=@SaleId;",new{debt=snapshot.DebtReduction,r.SaleId});
            if(snapshot.CashRefund>0)
            {
                await w.Authorize(userId,"CAJA.VER","CAJA.MOVIMIENTO");
                await w.Execute("INSERT INTO ven.MovimientosCaja(IdTurno,Tipo,Monto,ReferenciaTipo,ReferenciaId,IdUsuario,Motivo) VALUES(@ShiftId,N'DEVOLUCION_EFECTIVO',@cash,N'DEVOLUCION',@ReturnId,@userId,@reason);",new{r.ShiftId,cash=snapshot.CashRefund,r.ReturnId,userId,reason=r.Reason});await TouchShift(w,r.ShiftId!.Value);
            }
        }
        var authorization=await w.Scalar<long>("INSERT INTO sis.AutorizacionesOperacion(TipoOperacion,EntidadTipo,EntidadId,SolicitadoPor,AutorizadoPor,Motivo,FechaAutorizacion,Resultado,UtilizadaMvp) OUTPUT INSERTED.IdAutorizacion SELECT CASE WHEN EsAnulacion=1 THEN N'ANULACION_VENTA' ELSE N'DEVOLUCION' END,N'DEVOLUCION',IdDevolucion,SolicitadaPor,@administratorId,@Reason,SYSDATETIMEOFFSET(),@result,1 FROM ven.DevolucionesVenta WHERE IdDevolucion=@ReturnId;",new{administratorId,r.Reason,result=r.Apply?"APROBADA":"RECHAZADA",r.ReturnId});
        await w.Execute("UPDATE ven.DevolucionesVenta SET Estado=@state,AutorizadaPor=@administratorId,FechaAutorizacion=SYSDATETIMEOFFSET(),IdAutorizacion=@authorization,MotivoResolucion=@Reason,MontoReintegro=@amount,ReduccionDeuda=@debt,ReintegroEfectivo=@cash,IdTurno=@shift,FechaAplicacion=CASE WHEN @apply=1 THEN SYSDATETIMEOFFSET() ELSE NULL END WHERE IdDevolucion=@ReturnId;",new{state=r.Apply?"APLICADA":"RECHAZADA",administratorId,authorization,r.Reason,amount=snapshot?.CurrentAmount??request.Amount,debt=snapshot?.DebtReduction??0,cash=snapshot?.CashRefund??0,shift=snapshot?.CashRefund>0?r.ShiftId:null,apply=r.Apply,r.ReturnId});
        if(r.Apply)
        {
            var available=await ReturnableLines(w,r.SaleId);
            await w.Execute("UPDATE ven.Ventas SET Estado=@state,SaldoPendiente=SaldoPendiente-@debt WHERE IdVenta=@SaleId;",new{state=request.CancelSale?"ANULADA":available.All(x=>x.Remaining==0)?"DEVUELTA_TOTAL":"DEVUELTA_PARCIAL",debt=snapshot!.DebtReduction,r.SaleId});
            if(order is not null)
            {
                var next=request.CancelSale?"CANCELADO":order.State is "ENTREGADO" or "CANCELADO"?order.State:"INCIDENCIA";
                await w.Execute("UPDATE ven.PedidosMvp SET Estado=@next WHERE IdPedido=@Id;",new{next,order.Id});
                var eventReason=$"Posventa {request.Number}; anterior {order.State}. {r.Reason}";
                await OrderEvent(w,order.Id,userId,next,eventReason[..Math.Min(500,eventReason.Length)]);
            }
            if(request.CancelSale)await w.Execute("UPDATE fis.DocumentosFiscales SET Estado=N'ANULADO' WHERE IdVenta=@SaleId AND Estado=N'PENDIENTE';",r);
        }
        return r.ReturnId;
    },ct,OperationIdentity.Describe(r));
    private static async Task RestockReturn(Work w,SaleReturnLineData line,int boxId,long returnId,int userId)
    {
        var warehouse=await BoxWarehouse(w,boxId);
        var source=await w.One<ReturnStockSource>("SELECT d.IdProducto ProductId,d.IdLote LotId,d.CostoUnitario Cost,p.Activo Active FROM ven.VentaDetalle d JOIN inv.Productos p WITH(UPDLOCK,HOLDLOCK) ON p.IdProducto=d.IdProducto WHERE d.IdDetalle=@SaleLineId;",line);
        Require(source.Active,"INVALID_STATE","Un producto inactivo no puede volver al stock vendible.");
        if(source.LotId is {} lotId)Require(await w.Scalar<bool>("SELECT CONVERT(bit,CASE WHEN EXISTS(SELECT 1 FROM inv.LotesProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdLote=@lotId AND IdProducto=@ProductId AND Activo=1 AND (FechaVencimiento IS NULL OR FechaVencimiento>=CONVERT(date,SYSDATETIMEOFFSET() AT TIME ZONE 'Central America Standard Time'))) THEN 1 ELSE 0 END);",new{lotId,source.ProductId}),"INVALID_STATE","El lote está inactivo o vencido; solicita devolución sin reingreso vendible.");
        var stock=await w.Maybe<ReceivingStock>("SELECT StockActual Quantity,CostoPromedio Cost FROM inv.ExistenciasProducto WITH(UPDLOCK,HOLDLOCK) WHERE IdProducto=@ProductId AND IdAlmacen=@warehouse;",new{source.ProductId,warehouse});
        var before=stock?.Quantity??0;var after=before+line.Quantity;var average=PurchaseRules.Average(before,stock?.Cost??0,line.Quantity,source.Cost);
        if(stock is null)await w.Execute("INSERT INTO inv.ExistenciasProducto(IdAlmacen,IdProducto,StockActual,CostoPromedio,FechaUltimoMovimiento) VALUES(@warehouse,@ProductId,@after,@average,SYSDATETIMEOFFSET());",new{warehouse,source.ProductId,after,average});
        else await w.Execute("UPDATE inv.ExistenciasProducto SET StockActual=@after,CostoPromedio=@average,FechaUltimoMovimiento=SYSDATETIMEOFFSET() WHERE IdAlmacen=@warehouse AND IdProducto=@ProductId;",new{after,average,warehouse,source.ProductId});
        if(source.LotId is {} lot)
        {
            var exists=await w.Scalar<int>("SELECT COUNT(*) FROM inv.ExistenciasLote WITH(UPDLOCK,HOLDLOCK) WHERE IdAlmacen=@warehouse AND IdLote=@lot;",new{warehouse,lot});
            if(exists==0)await w.Execute("INSERT INTO inv.ExistenciasLote(IdAlmacen,IdLote,StockActual) VALUES(@warehouse,@lot,@Quantity);",new{warehouse,lot,line.Quantity});
            else await w.Execute("UPDATE inv.ExistenciasLote SET StockActual=StockActual+@Quantity WHERE IdAlmacen=@warehouse AND IdLote=@lot;",new{line.Quantity,warehouse,lot});
        }
        var movement=await w.Scalar<long>("INSERT INTO inv.MovimientosInventario(IdAlmacen,IdProducto,IdLote,TipoMovimiento,Cantidad,CostoUnitario,ExistenciaAnterior,ExistenciaPosterior,ReferenciaTipo,ReferenciaId,IdUsuario,Observacion) OUTPUT INSERTED.IdMovimiento VALUES(@warehouse,@ProductId,@LotId,N'DEVOLUCION',@Quantity,@Cost,@before,@after,N'DEVOLUCION',@returnId,@userId,N'Reingreso vendible al costo histórico de venta.');",new{warehouse,source.ProductId,source.LotId,line.Quantity,source.Cost,before,after,returnId,userId});
        await w.Execute("UPDATE ven.DevolucionDetalle SET IdMovimiento=@movement WHERE IdDetalle=@Id;",new{movement,line.Id});
    }
    private sealed class ReturnStockSource{public int ProductId{get;set;}public long? LotId{get;set;}public decimal Cost{get;set;}public bool Active{get;set;}}
}
