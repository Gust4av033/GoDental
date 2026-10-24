using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
internal sealed class StageTwoUiRepository:IReportRepository,IDeliveryRepository
{
    public Task<OperationalReportData>? PendingReport {get;set;}
    public bool LoseResponse {get;set;}
    public List<object> Writes {get;}=[];
    public DeliveryData? Delivery {get;set;}
    public Task<OperationalReportData> ReportAsync(ReportFilter f,int userId,CancellationToken ct)=>PendingReport??Task.FromResult(new OperationalReportData(f,DateTimeOffset.UtcNow,true,[new(){Date=DateTimeOffset.UtcNow,Document="V-001",Product="Producto",Category="Categoría",Party="Cliente",Quantity=2,Amount=23,Tax=2,Delivery=1,Cost=8,Stock=10,Sellable=10,Minimum=2,Value=40,InRoute=2,RouteValue=8,Warehouse="Bodega"}]));
    public Task<SaleQuote> QuoteDeliveryAsync(long order,int box,int user,CancellationToken ct)=>Task.FromResult(new SaleQuote(new(box,1,[new(1,2)]),[],20,0,0,20,7,35,"TOKEN"));
    public Task<DeliveryData?> DeliveryAsync(long order,int user,CancellationToken ct)=>Task.FromResult(Delivery);
    private Task<Result<MvpReceipt>> Save(object r,Guid id){Writes.Add(r);if(LoseResponse)throw new System.IO.IOException("Prueba: respuesta perdida");return Task.FromResult(Result<MvpReceipt>.Success(new(1,id)));}
    public Task<Result<MvpReceipt>> DispatchDeliveryAsync(DispatchDelivery r,int user,int? station,CancellationToken ct)=>Save(r,r.OperationId);
    public Task<Result<MvpReceipt>> CompleteDeliveryAsync(CompleteDelivery r,int user,int? station,CancellationToken ct)=>Save(r,r.OperationId);
    public Task<Result<MvpReceipt>> SettleDeliveryAsync(SettleDelivery r,int user,int? station,CancellationToken ct)=>Save(r,r.OperationId);
}
