using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Infrastructure.Operations;
using Microsoft.Extensions.Options;
namespace GoDental.Tests.Common;
public sealed class StageTwoTests
{
    [Fact]public void DeliveryRequestsFreezeVersionsAndHaveDistinctStableIdentities()
    {
        var r=new DispatchDelivery(Guid.NewGuid(),1,new byte[8],1,"TOKEN"," Repartidor "," Motivo ");var ticket=OperationIdentity.Describe(r);
        Assert.True(OperationIdentity.IsValid(ticket));Assert.Equal(ticket,OperationIdentity.Describe(r with{Courier="Repartidor",Reason="Motivo"}));
        var frozen=DeliveryValidator.Freeze(r);r.ExpectedVersion[0]=1;Assert.Equal(0,frozen.ExpectedVersion[0]);Assert.NotEqual(ticket,OperationIdentity.Describe(r));
        Assert.True(OperationIdentity.IsValid(OperationIdentity.Describe(new CompleteDelivery(Guid.NewGuid(),1,new byte[8],"INCIDENCIA",null,null,0,false,"Motivo"))));
        Assert.True(OperationIdentity.IsValid(OperationIdentity.Describe(new SettleDelivery(Guid.NewGuid(),1,new byte[8],1,20,null,"Motivo"))));
    }
    [Theory][InlineData("ENTREGADO","CREDITO",20,false)][InlineData("ENTREGADO","TRANSFERENCIA",20,false)][InlineData("ENTREGADO","EFECTIVO",0,false)][InlineData("RETORNADO",null,0,false)][InlineData("INCIDENCIA","EFECTIVO",20,false)][InlineData("DESCONOCIDO",null,0,false)]
    public void DeliveryRejectsCreditMissingTransferAndUnrecoveredReturns(string outcome,string? medium,decimal amount,bool recovered)=>Assert.NotNull(DeliveryValidator.Validate(new CompleteDelivery(Guid.NewGuid(),1,new byte[8],outcome,medium,null,amount,recovered,"Motivo")));
    [Fact]public async Task ReadPermissionDoesNotGrantDispatchOrSettlement()
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["VENTAS.VER"],false));var repo=new StageTwoUiRepository();var svc=new DeliveryService(repo,s);
        Assert.Null(await svc.ReadAsync(1));Assert.False(svc.CanManage);Assert.False(svc.CanSettle);
        Assert.Equal("FORBIDDEN",(await svc.DispatchAsync(new(Guid.NewGuid(),1,new byte[8],1,"TOKEN","Repartidor","Motivo"))).ErrorCode);Assert.Empty(repo.Writes);
    }
    [Fact]public void ReportGroupsReconcileAndDoNotRevealCostsWhenRestricted()
    {
        var today=DateTime.Today;var rows=new[]{new ReportRow{Date=DateTimeOffset.UtcNow,Product="A",Category="Uno",Party="Cliente",Document="V1",Amount=20,Tax=2,Cost=8},new ReportRow{Date=DateTimeOffset.UtcNow,Product="A",Category="Uno",Party="Cliente",Document="DEV1",Amount=-10,Tax=-1,Cost=-4},new ReportRow{Date=DateTimeOffset.UtcNow,Product="Entrega",Category="Entrega",Party="Cliente",Document="V1",Amount=3,Delivery=3,Cost=0}};
        foreach(var group in Enum.GetValues<ReportGrouping>())
        {
            var data=new OperationalReportData(new(OperationalReport.Sales,today,today,group),DateTimeOffset.UtcNow,true,rows);var grouped=ReportService.Group(data);
            Assert.Equal(data.Total,grouped.Sum(x=>x.Amount));Assert.Equal(data.Net,grouped.Sum(x=>x.Net));Assert.Equal(data.Cost,grouped.Sum(x=>x.Cost));Assert.Equal(5,data.Margin);
            var hidden=data with{CostsVisible=false};Assert.Null(hidden.Cost);Assert.Null(hidden.Margin);Assert.All(ReportService.Group(hidden),x=>Assert.Null(x.Cost));
        }
    }
    [Fact]public void ReportsBoundPeriodAndRespectLocalMidnight()
    {
        Assert.Throws<ArgumentException>(()=>ReportService.Validate(new(OperationalReport.Sales,new(2025,1,1),new(2026,1,2))));
        Assert.Throws<ArgumentException>(()=>ReportService.Validate(new(OperationalReport.Sales,DateTime.Today,DateTime.Today.AddDays(-1))));
        Assert.Equal(new DateTime(2026,10,22),new ReportRow{Date=new DateTimeOffset(2026,10,23,5,59,59,TimeSpan.Zero)}.LocalDate);
    }
    [Theory][InlineData(OperationalReport.Sales,215.9)][InlineData(OperationalReport.Sales,58)][InlineData(OperationalReport.Inventory,80)][InlineData(OperationalReport.Expiry,210)][InlineData(OperationalReport.Purchases,215.9)]
    public async Task ReportsRenderRealPdfAndConfigurablePreview(OperationalReport kind,double width)
    {
        var data=await new StageTwoUiRepository().ReportAsync(new(kind,DateTime.Today,DateTime.Today),1,default);
        var pdf=new OperationalReportRenderer(Options.Create(new ReceiptPdfOptions())).Render(data,new((decimal)width,279.4m,width<140?3:11));
        Assert.Equal("%PDF-",System.Text.Encoding.ASCII.GetString(pdf.Pdf,0,5));Assert.NotEmpty(pdf.Pages);Assert.Equal((decimal)width,pdf.WidthMm);
    }
    [Fact]public async Task ReportSessionChangeDiscardsPendingData()
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["REPORTES.VENTAS"],false));var repo=new StageTwoUiRepository();var pending=new TaskCompletionSource<OperationalReportData>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingReport=pending.Task;
        var svc=new ReportService(repo,new OperationalReportRenderer(Options.Create(new ReceiptPdfOptions())),s);var f=new ReportFilter(OperationalReport.Sales,DateTime.Today,DateTime.Today);var task=svc.ReadAsync(f);s.Clear();pending.SetResult(new(f,DateTimeOffset.UtcNow,false,[]));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>task);
    }
}
