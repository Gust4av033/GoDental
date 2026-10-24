using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Infrastructure.Security;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class ReturnTests
{
    private static RequestSaleReturn Request()=>new(Guid.NewGuid(),1,[1,2,3,4,5,6,7,8],false,"Factura 1","Defecto",[new(1,1,"SELLADO",true,null,[])]);
    [Fact]public void ValidatorRequiresProofPhotosAndNonSellableDestination()
    {
        var r=Request();Assert.Null(SaleReturnValidator.Validate(r));
        Assert.NotNull(SaleReturnValidator.Validate(r with{Proof=" "}));
        Assert.NotNull(SaleReturnValidator.Validate(r with{Items=[r.Items[0] with{Condition="ABIERTO_DEFECTUOSO",Restock=false,NoRestockReason="Defecto"}]}));
        Assert.NotNull(SaleReturnValidator.Validate(r with{Items=[r.Items[0] with{Condition="DAÑADO"}]}));
        Assert.NotNull(SaleReturnValidator.Validate(r with{Items=[r.Items[0] with{Restock=false}]}));
        Assert.NotNull(SaleReturnValidator.Validate(r with{Items=[r.Items[0],r.Items[0]]}));
    }
    [Fact]public void OperationIdentityFreezesQuantitiesAndEvidenceWithoutSecretsOrImageData()
    {
        var r=Request();var frozen=SaleReturnValidator.Freeze(r);var ticket=OperationIdentity.Describe(r);
        Assert.True(OperationIdentity.IsValid(ticket));Assert.Equal("SOLICITAR_DEVOLUCION",ticket.Type);Assert.Equal(ticket,OperationIdentity.Describe(frozen));
        r.ExpectedSaleVersion[0]=99;Assert.NotEqual(r.ExpectedSaleVersion,frozen.ExpectedSaleVersion);Assert.NotEqual(ticket,OperationIdentity.Describe(r));
        var resolve=new ResolveSaleReturn(Guid.NewGuid(),1,2,[1,2,3,4,5,6,7,8],true,new string('A',64),null,"Autorizada");
        Assert.Equal("APLICAR_DEVOLUCION",OperationIdentity.Describe(resolve).Type);Assert.Equal("RECHAZAR_DEVOLUCION",OperationIdentity.Describe(resolve with{Apply=false}).Type);
    }
    private static UserSession Session(params string[] permissions)
    {var session=new UserSession();session.Begin(new(1,"test","Prueba",1,["Administrador"],permissions.Length>0?permissions:["VENTAS.VER","VENTAS.DEVOLUCION_SOLICITAR","VENTAS.ANULAR","CAJA.VER","CAJA.MOVIMIENTO","VENTAS.REIMPRIMIR"],false,"returns-test"));return session;}
    [Fact]public async Task LostResponseCanBeRecoveredWithoutAnotherReturnWrite()
    {
        var repo=new MvpUiRepository{LoseResponse=true};var session=Session();var service=new SaleReturnService(repo,session,DispatchProxy.Create<IIdentityGateway,Admin>());var operation=new MvpOperationModel(session,new(repo,session),NullLogger.Instance);
        var r=Request();await operation.StartAsync(r,r.OperationId,x=>service.RequestAsync(x),_=>Task.CompletedTask);Assert.True(operation.HasPending);Assert.Single(repo.Writes);
        repo.FoundReceipt=new(401,r.OperationId);await operation.CheckCommand.ExecuteAsync(null);Assert.False(operation.HasPending);Assert.Single(repo.Writes);
    }
    [Fact]public async Task ReadOnlyUserCannotRequestOrReauthenticateAWrite()
    {
        var repo=new MvpUiRepository();var session=Session("VENTAS.VER");var service=new SaleReturnService(repo,session,DispatchProxy.Create<IIdentityGateway,Admin>());
        Assert.False((await service.RequestAsync(Request())).IsSuccess);Assert.Empty(repo.Writes);
        Assert.False((await service.ResolveAsync(new(Guid.NewGuid(),1,1,[1,2,3,4,5,6,7,8],false,"",null,"Rechazar"),"admin","secret")).IsSuccess);Assert.Empty(repo.Writes);
    }
    [Fact]public async Task SessionChangeDuringReauthenticationNeverUsesTheNewSessionToResolve()
    {
        var repo=new MvpUiRepository();var session=Session();var port=DispatchProxy.Create<IIdentityGateway,DelayedAdmin>();var delayed=(DelayedAdmin)(object)port;
        var service=new SaleReturnService(repo,session,port);var pending=service.ResolveAsync(new(Guid.NewGuid(),1,1,[1,2,3,4,5,6,7,8],false,"",null,"Rechazar"),"admin","secret");
        session.Clear();delayed.Pending.SetResult(Result<UserIdentity>.Success(new(2,"admin","Admin",1,["Administrador"],["VENTAS.DEVOLUCION_AUTORIZAR"],false)));
        Assert.False((await pending).IsSuccess);Assert.Empty(repo.Writes);
    }
    [Fact]public async Task HistoryDiscardsOldSessionReturnReadsAndDoesNotEnableExpiredRequests()
    {
        var repo=new MvpUiRepository();var session=Session();var identity=DispatchProxy.Create<IIdentityGateway,Admin>();var operations=new MvpOperationService(repo,session);var dialogs=new Dialogs();var customers=new CustomerWorkflowRepository();var sales=new SalesService(repo,session,identity);
        var pos=new PosViewModel(sales,new(repo,session),new(customers,session),new(repo,session),dialogs,session,operations,NullLogger<PosViewModel>.Instance);
        var files=new Files();var history=new CommercialHistoryViewModel(sales,new(repo,DispatchProxy.Create<IReceiptRenderer,MvpUiTests.PdfPort>(),session),files,files,pos,new NavigationService(),session,operations,NullLogger<CommercialHistoryViewModel>.Instance,returnService:new(repo,session,identity),returnDialogs:dialogs);
        repo.Sale.Sale.Date=DateTimeOffset.UtcNow;repo.Returns=repo.Returns with{ServerNow=DateTimeOffset.UtcNow};repo.Sale.Sale.RowVersion=[1,2,3,4,5,6,7,8];history.SelectedSale=repo.Sale.Sale;
        await history.ReadSaleCommand.ExecuteAsync(null);Assert.True(history.CanRequestReturn);
        repo.Returns=repo.Returns with{ServerNow=repo.Sale.Sale.Date.AddHours(25)};await history.ReloadReturnsCommand.ExecuteAsync(null);Assert.False(history.CanRequestReturn);
        var delayed=new TaskCompletionSource<SaleReturnsSnapshot>();repo.ReturnsReadTask=delayed.Task;var load=history.ReloadReturnsCommand.ExecuteAsync(null);session.Clear();delayed.SetResult(repo.Returns);await load;Assert.Null(history.SaleReturns);Assert.Null(history.SaleDetail);Assert.False(history.CanRequestReturn);
    }
    [Fact]public async Task UnwritableEvidenceFolderIsDefinitiveAndLeavesNoPendingOperation()
    {
        var repo=new MvpUiRepository();var session=Session("VENTAS.VER","AJUSTES.EDITAR");var service=new SaleReturnService(repo,session,DispatchProxy.Create<IIdentityGateway,Admin>(),DispatchProxy.Create<IReturnEvidenceStore,BrokenEvidence>());
        var operation=new MvpOperationModel(session,new(repo,session),NullLogger.Instance);var request=new ConfigureReturnEvidence(Guid.NewGuid(),"C:\\Evidencias",[1,2,3,4,5,6,7,8],"Carpeta para pruebas");
        await operation.StartAsync(request,request.OperationId,x=>service.ConfigureAsync(x),_=>Task.CompletedTask);
        Assert.False(operation.HasPending);Assert.Equal("INVALID_STATE",operation.LastErrorCode);Assert.Empty(repo.Writes);
    }
    public class BrokenEvidence:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>throw new IOException("Sin acceso a la carpeta compartida.");}
    public class Admin:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>Task.FromResult(Result<UserIdentity>.Success(new(2,"admin","Admin",1,["Administrador"],["VENTAS.DEVOLUCION_AUTORIZAR"],false)));}
    public class DelayedAdmin:DispatchProxy{public TaskCompletionSource<Result<UserIdentity>> Pending{get;}=new();protected override object? Invoke(MethodInfo? method,object?[]? args)=>Pending.Task;}
    private sealed class Dialogs:IMvpDialogService{public Task<bool> ShowAsync(MvpFormModel form)=>Task.FromResult(false);}
    private sealed class Files:IPdfFileService,IPdfPrintService{public Task<bool> SaveAsync(byte[] pdf,string name)=>Task.FromResult(false);public Task<bool> PrintAsync(ReceiptDocument pdf,string name)=>Task.FromResult(false);}
}
