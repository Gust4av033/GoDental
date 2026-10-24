using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
namespace GoDental.Application.Services;
public sealed class ReportService(IReportRepository repository,IReportRenderer renderer,IUserSession session):MvpServiceAccess(session)
{
    public bool CanRead=>CanReadKind(OperationalReport.Sales)||CanReadKind(OperationalReport.Inventory)||CanReadKind(OperationalReport.Purchases);
    public bool CanPrint=>Has("VENTAS.VER","VENTAS.REIMPRIMIR");
    public bool CanReadKind(OperationalReport kind)=>kind switch
    {
        OperationalReport.Sales=>Has("REPORTES.VENTAS"),
        OperationalReport.Inventory or OperationalReport.Expiry=>Has("REPORTES.INVENTARIO"),
        OperationalReport.Purchases=>Has("REPORTES.COSTOS","INVENTARIO.COSTOS_VER","COMPRAS.VER"), _=>false
    };
    public static void Validate(ReportFilter f)
    {
        if(!Enum.IsDefined(f.Kind)||!Enum.IsDefined(f.Group)||f.From.Date>f.To.Date||f.To.Date-f.From.Date>TimeSpan.FromDays(365)||f.From.Year<2000||f.To.Year>9998)
            throw new ArgumentException("Selecciona un período válido de hasta 366 días y un reporte disponible.");
    }
    public async Task<OperationalReportData> ReadAsync(ReportFilter f,CancellationToken ct=default)
    {
        Validate(f);if(!CanReadKind(f.Kind))throw new UnauthorizedAccessException("No tienes permiso para este reporte.");
        var revision=CurrentSession is ISessionController s?s.Revision:0;
        var result=await repository.ReportAsync(f with{From=f.From.Date,To=f.To.Date},UserId,ct);
        if(!CanReadKind(f.Kind)||CurrentSession is ISessionController current&&current.Revision!=revision)throw new UnauthorizedAccessException("La sesión cambió durante la consulta.");
        return result;
    }
    public async Task<ReceiptDocument> PreviewAsync(ReportFilter f,PrintLayout layout,CancellationToken ct=default)
    {
        Read("VENTAS.VER","VENTAS.REIMPRIMIR");layout.Validate(layout.WidthMm<140);
        var revision=CurrentSession is ISessionController s?s.Revision:0;var data=await ReadAsync(f,ct);
        var pdf=await Task.Run(()=>renderer.Render(data,layout),ct);
        Read("VENTAS.VER","VENTAS.REIMPRIMIR");if(!CanReadKind(f.Kind)||CurrentSession is ISessionController current&&current.Revision!=revision)throw new UnauthorizedAccessException("La sesión cambió durante la generación.");
        return pdf;
    }
    public static IReadOnlyList<ReportRow> Group(OperationalReportData data)
    {
        if(data.Filter.Kind!=OperationalReport.Sales)return data.Rows;
        string Label(ReportRow x)=>data.Filter.Group switch{ReportGrouping.Day=>x.LocalDate.ToString("dd/MM/yyyy"),ReportGrouping.Product=>x.Product,ReportGrouping.Category=>x.Category,ReportGrouping.Customer=>x.Party,_=>x.Document};
        return data.Rows.GroupBy(x=>data.Filter.Group switch{ReportGrouping.Product=>x.ProductId!=0?x.ProductId.ToString():x.Product,ReportGrouping.Category=>x.CategoryId!=0?x.CategoryId.ToString():x.Category,ReportGrouping.Customer=>x.CustomerId!=0?x.CustomerId.ToString():x.Party,_=>Label(x)})
            .Select(g=>new ReportRow{GroupLabel=Label(g.First()),Quantity=g.Sum(x=>x.Quantity),Amount=g.Sum(x=>x.Amount),Tax=g.Sum(x=>x.Tax),Delivery=g.Sum(x=>x.Delivery),Cost=data.CostsVisible?g.Sum(x=>x.Cost??0):null})
            .OrderByDescending(x=>x.Amount).ThenBy(x=>x.GroupLabel,StringComparer.Ordinal).ToArray();
    }
}
