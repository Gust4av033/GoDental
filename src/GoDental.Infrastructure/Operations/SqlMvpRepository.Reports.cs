using GoDental.Application.DTOs;
using GoDental.Application.Services;
namespace GoDental.Infrastructure.Operations;
public sealed partial class SqlMvpRepository
{
    public Task<OperationalReportData> ReportAsync(ReportFilter f,int userId,CancellationToken ct)
    {
        ReportService.Validate(f);
        string[] permissions=f.Kind switch{OperationalReport.Sales=>["REPORTES.VENTAS"],OperationalReport.Inventory or OperationalReport.Expiry=>["REPORTES.INVENTARIO"],OperationalReport.Purchases=>["REPORTES.COSTOS","INVENTARIO.COSTOS_VER","COMPRAS.VER"],_=>throw new ArgumentException("Reporte desconocido.")};
        return Read(userId,permissions,async w=>
        {
            var costs=await w.Scalar<bool>("""
                SELECT CONVERT(bit,CASE WHEN COUNT(DISTINCT p.Codigo)=2 THEN 1 ELSE 0 END) FROM seg.UsuarioRoles ur
                JOIN seg.Roles r ON r.IdRol=ur.IdRol AND r.Activo=1 JOIN seg.RolPermisos rp ON rp.IdRol=r.IdRol AND rp.Concedido=1
                JOIN seg.Permisos p ON p.IdPermiso=rp.IdPermiso AND p.Activo=1 WHERE ur.IdUsuario=@userId AND p.Codigo IN(N'REPORTES.COSTOS',N'INVENTARIO.COSTOS_VER');
                """,new{userId});
            var start=new DateTimeOffset(DateTime.SpecifyKind(f.From.Date,DateTimeKind.Unspecified),TimeSpan.FromHours(-6)).ToUniversalTime();
            var end=new DateTimeOffset(DateTime.SpecifyKind(f.To.Date.AddDays(1),DateTimeKind.Unspecified),TimeSpan.FromHours(-6)).ToUniversalTime();
            var now=await w.Scalar<DateTimeOffset>("SELECT SYSDATETIMEOFFSET();");
            var today=now.ToOffset(TimeSpan.FromHours(-6)).Date;
            var rows=await w.List<ReportRow>(f.Kind switch{OperationalReport.Sales=>SalesReportSql,OperationalReport.Inventory=>InventoryReportSql,OperationalReport.Expiry=>ExpiryReportSql,_=>PurchaseReportSql},new{start,end,costs,now,today});
            Require(rows.Count<=5000,"VALIDATION","El reporte supera 5000 filas. Reduce el período; no se mostrarán totales incompletos.");
            if(f.Kind==OperationalReport.Inventory)foreach(var row in rows)if(row.Note!="Series pendientes")row.Note=row.Sellable<=row.Minimum?"Revisar reposición":"";
            return new OperationalReportData(f,now,costs,rows);
        },ct);
    }
    private const string SalesReportSql="""
        SELECT TOP(5001) ProductId,CategoryId,CustomerId,Date,Document,Product,Category,Party,Quantity,Amount,Tax,Delivery,CASE WHEN @costs=1 THEN Cost END Cost,Note FROM(
          SELECT d.IdProducto ProductId,c.IdCategoria CategoryId,ISNULL(v.IdCliente,0) CustomerId,v.Fecha Date,v.NumeroVenta Document,d.DescripcionSnapshot Product,c.Nombre Category,
           COALESCE(JSON_VALUE(v.ClienteSnapshotMvp,'$.Name'),cl.NombreRazonSocial,N'Consumidor final') Party,
           d.Cantidad Quantity,d.TotalLinea Amount,d.ImpuestoMonto Tax,CONVERT(decimal(18,2),0) Delivery,d.Cantidad*d.CostoUnitario Cost,N'Venta' Note
          FROM ven.Ventas v JOIN ven.VentaDetalle d ON d.IdVenta=v.IdVenta JOIN inv.Productos p ON p.IdProducto=d.IdProducto
          JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria LEFT JOIN cli.Clientes cl ON cl.IdCliente=v.IdCliente
          WHERE v.Fecha>=@start AND v.Fecha<@end AND v.Estado IN(N'CONFIRMADA',N'DEVUELTA_PARCIAL',N'DEVUELTA_TOTAL',N'ANULADA')
          UNION ALL
          SELECT 0,0,ISNULL(v.IdCliente,0),v.Fecha,v.NumeroVenta,N'Servicio de entrega',N'Entrega',COALESCE(JSON_VALUE(v.ClienteSnapshotMvp,'$.Name'),cl.NombreRazonSocial,N'Consumidor final'),0,v.CargoEntrega,0,v.CargoEntrega,0,N'Cargo de entrega'
          FROM ven.Ventas v LEFT JOIN cli.Clientes cl ON cl.IdCliente=v.IdCliente WHERE v.Fecha>=@start AND v.Fecha<@end AND v.CargoEntrega>0 AND v.Estado IN(N'CONFIRMADA',N'DEVUELTA_PARCIAL',N'DEVUELTA_TOTAL',N'ANULADA')
          UNION ALL
          SELECT v.IdProducto,c.IdCategoria,ISNULL(s.IdCliente,0),r.FechaAplicacion,CONCAT(N'DEV-',r.IdDevolucion),v.DescripcionSnapshot,c.Nombre,
           COALESCE(JSON_VALUE(s.ClienteSnapshotMvp,'$.Name'),cl.NombreRazonSocial,N'Consumidor final'),
           -d.Cantidad,-d.MontoReintegrado,-(ROUND(CASE WHEN v.TotalLinea=0 THEN 0 ELSE (ISNULL(prev.Amount,0)+d.MontoReintegrado)*v.ImpuestoMonto/v.TotalLinea END,2)-ROUND(CASE WHEN v.TotalLinea=0 THEN 0 ELSE ISNULL(prev.Amount,0)*v.ImpuestoMonto/v.TotalLinea END,2)),0,
           CASE WHEN d.ReingresaInventario=1 THEN -d.Cantidad*v.CostoUnitario ELSE 0 END,N'Posventa aplicada'
          FROM ven.DevolucionesVenta r JOIN ven.DevolucionDetalle d ON d.IdDevolucion=r.IdDevolucion JOIN ven.VentaDetalle v ON v.IdDetalle=d.IdVentaDetalle
          JOIN ven.Ventas s ON s.IdVenta=r.IdVenta JOIN inv.Productos p ON p.IdProducto=v.IdProducto JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria
          LEFT JOIN cli.Clientes cl ON cl.IdCliente=s.IdCliente
          OUTER APPLY(SELECT SUM(dd.MontoReintegrado) Amount FROM ven.DevolucionDetalle dd JOIN ven.DevolucionesVenta rr ON rr.IdDevolucion=dd.IdDevolucion
           WHERE dd.IdVentaDetalle=d.IdVentaDetalle AND rr.Estado=N'APLICADA' AND (rr.FechaAplicacion<r.FechaAplicacion OR rr.FechaAplicacion=r.FechaAplicacion AND rr.IdDevolucion<r.IdDevolucion))prev
          WHERE r.Estado=N'APLICADA' AND r.FechaAplicacion>=@start AND r.FechaAplicacion<@end
          UNION ALL
          SELECT 0,0,ISNULL(s.IdCliente,0),r.FechaAplicacion,CONCAT(N'DEV-',r.IdDevolucion),N'Servicio de entrega',N'Entrega',COALESCE(JSON_VALUE(s.ClienteSnapshotMvp,'$.Name'),cl.NombreRazonSocial,N'Consumidor final'),
           0,-(r.MontoReintegro-ISNULL(x.Amount,0)),0,-(r.MontoReintegro-ISNULL(x.Amount,0)),0,N'Anulación de entrega'
          FROM ven.DevolucionesVenta r JOIN ven.Ventas s ON s.IdVenta=r.IdVenta LEFT JOIN cli.Clientes cl ON cl.IdCliente=s.IdCliente
          OUTER APPLY(SELECT SUM(d.MontoReintegrado) Amount FROM ven.DevolucionDetalle d WHERE d.IdDevolucion=r.IdDevolucion)x
          WHERE r.Estado=N'APLICADA' AND r.FechaAplicacion>=@start AND r.FechaAplicacion<@end AND r.MontoReintegro>ISNULL(x.Amount,0)
        )m ORDER BY Date,Document,Product;
        """;
    private const string InventoryReportSql="""
        SELECT TOP(5001) @now Date,p.SKU Document,p.Nombre Product,c.Nombre Category,a.Nombre Warehouse,
         ISNULL(e.StockActual,0) Stock,ISNULL(e.StockReservado,0) Reserved,p.StockMinimo Minimum,
         CASE WHEN p.ControlaSerie=1 OR p.ControlaLote=0 AND p.ControlaVencimiento=1 THEN 0
          WHEN p.ControlaLote=0 THEN ISNULL(e.StockActual-e.StockReservado,0)
          WHEN ISNULL(l.Sellable,0)>ISNULL(e.StockReservado,0) THEN ISNULL(l.Sellable,0)-ISNULL(e.StockReservado,0) ELSE 0 END Sellable,
         ISNULL(r.Quantity,0) InRoute,CASE WHEN @costs=1 THEN ISNULL(e.CostoPromedio,0) END Cost,
         CASE WHEN @costs=1 THEN ISNULL(e.StockActual*e.CostoPromedio,0) END Value,CASE WHEN @costs=1 THEN ISNULL(r.Value,0) END RouteValue,
         CASE WHEN p.ControlaSerie=1 THEN N'Series pendientes' WHEN ISNULL(e.StockActual-e.StockReservado,0)<=p.StockMinimo THEN N'Revisar reposición' ELSE N'' END Note
        FROM inv.Productos p CROSS JOIN inv.Almacenes a JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria
        LEFT JOIN inv.ExistenciasProducto e ON e.IdProducto=p.IdProducto AND e.IdAlmacen=a.IdAlmacen
        OUTER APPLY(SELECT SUM(el.StockActual) Sellable FROM inv.ExistenciasLote el JOIN inv.LotesProducto lp ON lp.IdLote=el.IdLote
         WHERE lp.IdProducto=p.IdProducto AND el.IdAlmacen=a.IdAlmacen AND lp.Activo=1 AND (lp.FechaVencimiento IS NULL OR lp.FechaVencimiento>=@today))l
        OUTER APPLY(SELECT SUM(d.Cantidad) Quantity,SUM(d.Cantidad*d.CostoUnitario) Value FROM ven.RepartoLineasMvp d JOIN ven.RepartosMvp r ON r.IdReparto=d.IdReparto
         WHERE d.IdProducto=p.IdProducto AND r.IdAlmacen=a.IdAlmacen AND r.Estado IN(N'EN_RUTA',N'INCIDENCIA'))r
        WHERE (p.Activo=1 OR ISNULL(e.StockActual,0)>0 OR ISNULL(r.Quantity,0)>0) AND (a.Activo=1 OR ISNULL(e.StockActual,0)>0 OR ISNULL(r.Quantity,0)>0)
        ORDER BY a.IdAlmacen,p.IdProducto;
        """;
    private const string ExpiryReportSql="""
        SELECT TOP(5001) @now Date,p.SKU Document,p.Nombre Product,c.Nombre Category,a.Nombre Warehouse,l.NumeroLote Lot,l.FechaVencimiento Expires,
         e.StockActual Quantity,CASE WHEN l.Activo=0 THEN N'Bloqueado' WHEN l.FechaVencimiento<@today THEN N'Vencido: no vender' ELSE N'Vence en 30 días: revisar salida' END Note,
         CASE WHEN @costs=1 THEN ep.CostoPromedio END Cost,CASE WHEN @costs=1 THEN e.StockActual*ep.CostoPromedio END Value
        FROM inv.ExistenciasLote e JOIN inv.LotesProducto l ON l.IdLote=e.IdLote JOIN inv.Productos p ON p.IdProducto=l.IdProducto
        JOIN inv.Almacenes a ON a.IdAlmacen=e.IdAlmacen JOIN cat.CategoriasProducto c ON c.IdCategoria=p.IdCategoria
        JOIN inv.ExistenciasProducto ep ON ep.IdProducto=p.IdProducto AND ep.IdAlmacen=e.IdAlmacen
        WHERE e.StockActual>0 AND l.FechaVencimiento<=DATEADD(day,30,@today) ORDER BY l.FechaVencimiento,a.IdAlmacen,p.IdProducto;
        """;
    private const string PurchaseReportSql="""
        SELECT TOP(5001) COALESCE(r.FechaAplicacionMvp,r.FechaRecepcion) Date,r.NumeroRecepcion Document,p.Nombre Product,pr.Nombre Party,a.Nombre Warehouse,
         d.CantidadRecibida Quantity,d.CantidadRecibida*d.CostoCompraUSD Cost,CASE WHEN r.IdImportacion IS NULL THEN d.CantidadRecibida*d.GastoAsignadoUSD ELSE d.GastoImportacionTotalUSD END Freight,
         d.CantidadRecibida*d.CostoCompraUSD+CASE WHEN r.IdImportacion IS NULL THEN d.CantidadRecibida*d.GastoAsignadoUSD ELSE d.GastoImportacionTotalUSD END Amount,
         CASE WHEN r.FechaAplicacionMvp IS NULL THEN N'Histórico: fecha de recepción; aplicación no disponible' ELSE N'Recepción aplicada' END Note
        FROM cmp.RecepcionesCompra r JOIN cmp.RecepcionCompraDetalle d ON d.IdRecepcion=r.IdRecepcion JOIN inv.Productos p ON p.IdProducto=d.IdProducto
        JOIN cmp.OrdenesCompra o ON o.IdOrdenCompra=r.IdOrdenCompra JOIN cmp.Proveedores pr ON pr.IdProveedor=o.IdProveedor JOIN inv.Almacenes a ON a.IdAlmacen=r.IdAlmacen
        WHERE r.Estado=N'APLICADA' AND COALESCE(r.FechaAplicacionMvp,r.FechaRecepcion)>=@start AND COALESCE(r.FechaAplicacionMvp,r.FechaRecepcion)<@end
        ORDER BY COALESCE(r.FechaAplicacionMvp,r.FechaRecepcion),r.IdRecepcion,d.IdDetalle;
        """;
}
