using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class FiscalTests
{
    private static ReviewFiscalInternal Request()=>new(Guid.NewGuid(),1,new byte[8],new string('A',64),"REVISADO"," Revisión interna ");
    private static (FiscalViewModel Vm,UserSession Session,FiscalUiRepository Repo,Dialogs Dialogs) Create(params string[] permissions)
    {
        var session=new UserSession();session.Begin(new(1,"test","Prueba",1,[],permissions.Length>0?permissions:["FISCAL.VER","VENTAS.VER","FISCAL.CONFIGURAR","FISCAL.REVISAR_INTERNO"],false));
        var repo=new FiscalUiRepository();var dialogs=new Dialogs();return(new(new(repo,session),new(repo,session),dialogs,session,new(new MvpUiRepository(),session),NullLogger<FiscalViewModel>.Instance),session,repo,dialogs);
    }
    [Theory][InlineData("ACEPTADO")][InlineData("GENERADO")][InlineData("PENDIENTE")][InlineData("")]
    public void ReviewCannotSetFiscalStates(string outcome)=>Assert.NotNull(FiscalWorkspaceService.Validate(Request() with{Outcome=outcome}));
    [Fact] public void VersionTokenReasonAndOperationAreRequired()
    {
        var r=Request();Assert.Null(FiscalWorkspaceService.Validate(r));
        foreach(var bad in new[]{r with{OperationId=Guid.Empty},r with{DocumentId=0},r with{ExpectedVersion=[]},r with{ReviewToken=new string('Z',64)},r with{Reason=" "},r with{Reason=new string('a',501)}})Assert.NotNull(FiscalWorkspaceService.Validate(bad));
        var frozen=FiscalWorkspaceService.Freeze(r);var identity=OperationIdentity.Describe(r);Assert.True(OperationIdentity.IsValid(identity));Assert.Equal(identity,OperationIdentity.Describe(frozen));
        r.ExpectedVersion[0]=1;Assert.Equal(0,frozen.ExpectedVersion[0]);Assert.NotEqual(identity,OperationIdentity.Describe(r));
    }
    [Fact] public void ReviewBecomesStaleWithoutChangingFiscalState()
    {
        var detail=new FiscalUiRepository().Detail;Assert.Empty(detail.Issues);Assert.False(detail.ReviewCurrent);
        detail.Document.ReviewState="REVISADO";detail.Document.ReviewedFingerprint=detail.Fingerprint;Assert.True(detail.ReviewCurrent);
        var stale=detail with{Fingerprint=new string('C',64)};Assert.False(stale.ReviewCurrent);Assert.Contains("cambiaron",stale.ReviewLabel);Assert.Equal("PENDIENTE",detail.Document.State);
        detail.Document.Type="CCFE";Assert.NotEmpty(detail.Issues);detail.Receiver.Active=true;detail.Receiver.Nit="PRUEBA";detail.Receiver.Nrc="PRUEBA";detail.Receiver.Address="Dirección sintética";Assert.Empty(detail.Issues);
        Assert.NotEmpty((detail with{DteCount=1}).Issues);detail.Sale.State="ANULADA";Assert.NotEmpty(detail.Issues);
    }
    [Fact] public async Task ReadPermissionDoesNotGrantReviewOrCreation()
    {
        var(vm,session,repo,_)=Create("FISCAL.VER","VENTAS.VER");await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Documents.Single();await vm.OpenCommand.ExecuteAsync(null);
        Assert.False(vm.CanReview);await vm.FindSalesCommand.ExecuteAsync(null);vm.SelectedSale=vm.Candidates.Single();Assert.False(vm.CanCreate);
        var svc=new FiscalWorkspaceService(repo,session);Assert.Equal("FORBIDDEN",(await svc.ReviewAsync(Request())).ErrorCode);Assert.Empty(repo.Writes);
        session.Clear();Assert.Empty(vm.Documents);Assert.Empty(vm.Candidates);Assert.Empty(vm.History);Assert.Null(vm.Detail);Assert.False(vm.CanRead);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>svc.DetailAsync(1));
    }
    [Fact] public async Task LateDetailCannotReplaceSelectionOrExposePreviousSession()
    {
        var(vm,session,repo,_)=Create();await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Documents.Single();var pending=new TaskCompletionSource<FiscalDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingDetail=pending.Task;
        var read=vm.OpenCommand.ExecuteAsync(null);vm.Selected=new(){Id=2};pending.SetResult(repo.Detail);await read;Assert.Null(vm.Detail);Assert.Empty(vm.History);
        vm.Selected=repo.Detail.Document;pending=new(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingDetail=pending.Task;read=vm.OpenCommand.ExecuteAsync(null);session.Clear();pending.SetResult(repo.Detail);await read;Assert.Null(vm.Detail);Assert.Empty(vm.Documents);
    }
    [Fact] public async Task LateSearchCannotReplaceChangedFilters()
    {
        var(vm,_,repo,_)=Create();var pending=new TaskCompletionSource<MvpPage<FiscalDocumentData>>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingSearch=pending.Task;
        var read=vm.ReloadCommand.ExecuteAsync(null);vm.Search="Otra venta";pending.SetResult(new([repo.Detail.Document],1));await read;Assert.Empty(vm.Documents);
    }
    [Fact] public async Task LostReviewResponseRetainsExactlyTheSameRequest()
    {
        var(vm,_,repo,dialogs)=Create();await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Documents.Single();await vm.OpenCommand.ExecuteAsync(null);repo.LoseResponse=true;
        dialogs.Action=async form=>{Assert.Equal("REVISADO",form.Choice<FiscalViewModel.FiscalChoice>("outcome").Code);form.Fields.Single(f=>f.Key=="reason").Value="Datos revisados";form.Confirmed=true;await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);};
        await vm.ReviewCommand.ExecuteAsync(null);Assert.True(vm.Operation.HasPending);Assert.Single(repo.Writes);var first=OperationIdentity.Describe(repo.Writes[0]);repo.LoseResponse=false;await vm.Operation.RetryCommand.ExecuteAsync(null);
        Assert.False(vm.Operation.HasPending);Assert.Equal(2,repo.Writes.Count);Assert.All(repo.Writes,r=>Assert.Equal(first,OperationIdentity.Describe(r)));Assert.Single(vm.History);
    }
    [Fact] public async Task FiscalReadAlsoRequiresCommercialPermissionAndRejectsUnboundedFilters()
    {
        var(_,session,repo,_)=Create("FISCAL.VER");var service=new FiscalWorkspaceService(repo,session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SearchAsync("",null,new()));
        session.Begin(new(1,"test","Prueba",1,[],["FISCAL.VER","VENTAS.VER"],false));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SearchAsync("", "ACEPTADO",new()));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SearchAsync(new string('x',201),null,new()));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SearchAsync("",null,new(0)));
    }
    [Fact] public async Task FiscalServiceRejectsResultsFromPreviousIdentityEvenWithSamePermissions()
    {
        var(_,session,repo,_)=Create();var service=new FiscalWorkspaceService(repo,session);var pending=new TaskCompletionSource<FiscalDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);repo.PendingDetail=pending.Task;
        var read=service.DetailAsync(1);session.Begin(new(2,"other","Otra identidad",1,[],["FISCAL.VER","VENTAS.VER"],false));pending.SetResult(repo.Detail);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>read);
    }
    private sealed class Dialogs:IMvpDialogService {public Func<MvpFormModel,Task>? Action;public async Task<bool> ShowAsync(MvpFormModel f){if(Action is not null)await Action(f);return false;}}
}
