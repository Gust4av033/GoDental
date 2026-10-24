using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class ReportUiTests
{
    private static (ReportsViewModel Vm,UserSession Session,StageTwoUiRepository Repo,Files Files) Create()
    {
        var session=new UserSession();session.Begin(new(1,"test","Prueba",1,[],["REPORTES.VENTAS","REPORTES.INVENTARIO","VENTAS.VER","VENTAS.REIMPRIMIR"],false));
        var repo=new StageTwoUiRepository();var files=new Files();var vm=new ReportsViewModel(new(repo,new Renderer(),session),files,files,session,new(new MvpUiRepository(),session),NullLogger<ReportsViewModel>.Instance);return(vm,session,repo,files);
    }
    [Fact]public async Task FiltersAndPaperInvalidateReviewedDocumentAndSessionClearsCosts()
    {
        var(vm,session,_,files)=Create();await vm.ReloadCommand.ExecuteAsync(null);Assert.Single(vm.Rows);
        await vm.PreviewReceiptCommand.ExecuteAsync(null);var pdf=vm.ReceiptPreview;Assert.True(vm.CanUsePreview);
        await vm.ExportCommand.ExecuteAsync(null);await vm.PrintReceiptCommand.ExecuteAsync(null);Assert.Same(pdf!.Pdf,files.Pdf);Assert.Same(pdf,files.Document);Assert.Same(pdf,vm.ReceiptPreview);
        vm.PrintSettings.Paper="Ticket 58 mm";Assert.False(vm.CanUsePreview);files.Pdf=null;await vm.ExportCommand.ExecuteAsync(null);Assert.Null(files.Pdf);
        await vm.PreviewReceiptCommand.ExecuteAsync(null);Assert.True(vm.CanUsePreview);vm.To=vm.To!.Value.AddDays(1);Assert.Null(vm.ReceiptPreview);Assert.Empty(vm.Rows);
        await vm.ReloadCommand.ExecuteAsync(null);Assert.Single(vm.Rows);session.Clear();Assert.Empty(vm.Rows);Assert.Null(vm.Snapshot);Assert.Null(vm.ReceiptPreview);Assert.False(vm.CanRead);
    }
    [Fact]public async Task LateQueryCannotReplaceChangedFilters()
    {
        var(vm,_,repo,_)=Create();var pending=new TaskCompletionSource<OperationalReportData>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingReport=pending.Task;
        var filter=new ReportFilter(OperationalReport.Sales,vm.From!.Value,vm.To!.Value);var task=vm.ReloadCommand.ExecuteAsync(null);vm.Kind=vm.Kinds.Last();
        pending.SetResult(new(filter,DateTimeOffset.UtcNow,true,[new(){Product="Respuesta vieja",Cost=123}]));await task;Assert.Null(vm.Snapshot);Assert.Empty(vm.Rows);
    }
    [Fact]public async Task LostDeliveryResponseRetainsSameRequestAcrossRetry()
    {
        var(_,session,repo,_)=Create();session.Begin(new(1,"test","Prueba",1,[],["VENTAS.VER","VENTAS.CREAR","VENTAS.REPARTO_GESTIONAR"],false));
        var service=new DeliveryService(repo,session);var operation=new MvpOperationModel(session,new(new MvpUiRepository(),session),NullLogger.Instance);repo.LoseResponse=true;
        var request=new DispatchDelivery(Guid.NewGuid(),1,new byte[8],1,"TOKEN","Repartidor","Motivo");await operation.StartAsync(request,request.OperationId,q=>service.DispatchAsync(q),_=>Task.CompletedTask);
        Assert.True(operation.HasPending);Assert.Single(repo.Writes);repo.LoseResponse=false;await operation.RetryCommand.ExecuteAsync(null);
        Assert.False(operation.HasPending);Assert.Equal(2,repo.Writes.Count);Assert.All(repo.Writes,q=>Assert.Equal(GoDental.Application.Common.OperationIdentity.Describe(request),GoDental.Application.Common.OperationIdentity.Describe(q)));
    }
    private sealed class Renderer:IReportRenderer {public ReceiptDocument Render(OperationalReportData data,PrintLayout layout)=>new([1,2],[[1]],layout.WidthMm,layout.HeightMm,layout.MarginMm);}
    private sealed class Files:IPdfFileService,IPdfPrintService
    {
        public byte[]? Pdf;public ReceiptDocument? Document;
        public Task<bool> SaveAsync(byte[] pdf,string name){Pdf=pdf;return Task.FromResult(false);}
        public Task<bool> PrintAsync(ReceiptDocument document,string name){Document=document;return Task.FromResult(false);}
    }
}
