using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class ImportTests
{
    [Fact]public void LargestRemainderConservesCentsAndUsesStableTieBreaks()
    {
        var r=ImportRules.Allocate(1.01m,[(2,60m),(1,30m)]);Assert.Equal(.34m,r[1]);Assert.Equal(.67m,r[2]);
        var tie=ImportRules.Allocate(.02m,[(3,1m),(2,1m),(1,1m)]);Assert.Equal(.01m,tie[1]);Assert.Equal(.01m,tie[2]);Assert.Equal(0,tie[3]);
        Assert.Equal(1.01m,r.Values.Sum());Assert.Equal(r.OrderBy(x=>x.Key),ImportRules.Allocate(1.01m,[(1,30m),(2,60m)]).OrderBy(x=>x.Key));
    }
    [Fact]public void FreeGoodsDoNotSilentlyReceiveAnotherAllocationBasis()
    {
        Assert.Throws<ArgumentException>(()=>ImportRules.Allocate(1,[(1,0m)]));Assert.Equal(0,ImportRules.Allocate(0,[(1,0m)])[1]);
        var r=ImportRules.Allocate(.01m,[(1,0m),(2,100m)]);Assert.Equal(0,r[1]);Assert.Equal(.01m,r[2]);
    }
    [Fact]public void PartialBudgetsConsumeExactRemainderAndRejectExcess()
    {
        var spent=0m;for(var n=0;n<3;n++)spent+=ImportRules.PartialExpense(.34m,3,n,1,spent);Assert.Equal(.34m,spent);
        Assert.Throws<ArgumentException>(()=>ImportRules.PartialExpense(1,3,2,2,.6m));
        Assert.Equal(.23m,ImportRules.PartialExpense(.34m,3,1,2,.11m));
    }
    [Theory][InlineData("CONFIRMADA","DESPACHADA",true)][InlineData("DESPACHADA","EN_TRANSITO",true)][InlineData("ADUANA","EN_TRANSITO",false)][InlineData("EN_TRANSITO","RECIBIDA",false)][InlineData("RECIBIDA_PARCIAL","RECIBIDA",false)][InlineData("RECIBIDA","RECIBIDA",true)]
    public void LogisticsCannotInventPhysicalReceipts(string state,string next,bool expected)=>Assert.Equal(expected,ImportRules.CanMove(state,next));
    [Fact]public void IdentityNormalizesInputsAndFreezesVersions()
    {
        var v=new byte[]{1,2,3,4,5,6,7,8};var r=new SaveImportation(Guid.NewGuid(),1,v," Courier ","  ",null," Nota ");
        var frozen=(SaveImportation)ImportValidator.Freeze(r);Assert.Null(frozen.Guide);v[0]=9;Assert.Equal(1,frozen.ExpectedOrderVersion[0]);
        Assert.Equal(OperationIdentity.Describe(frozen),OperationIdentity.Describe(frozen with{Courier=" Courier",Reason="Nota "}));
        Assert.True(OperationIdentity.IsValid(OperationIdentity.Describe(frozen)));
        Assert.NotNull(ImportValidator.Validate(frozen with{Reason=new string('R',301)}));
    }
    private static UserSession Session()
    {var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["COMPRAS.VER","COMPRAS.GESTIONAR"],false,"imports|test"));return s;}
    private static ImportationSnapshot Snapshot(bool closed=false)=>new(new(){Id=301,OrderId=101,State="CONFIRMADA",Rate=closed?1:null,CostsClosed=closed,RowVersion=[1,2,3,4,5,6,7,8]},[new(){Id=7,Amount=1.01m,AmountUsd=1.01m,Type="FLETE",Currency="USD",Rate=1}],[]);
    [Fact]public void CostPreviewInvalidatesWhenRateChangesAndLocksReceiptCosts()
    {
        var repo=new MvpUiRepository();var draft=new ImportCostDraftModel(repo.Purchase,Snapshot());draft.CalculateCommand.Execute(null);
        Assert.Equal(1,draft.ReviewedRate);Assert.Equal(1.01m,draft.Items.Sum(x=>x.ExpenseUsd));draft.Rate="2";Assert.Null(draft.ReviewedRate);Assert.Empty(draft.Items);
        var receipt=new PurchaseReceiptDraftModel(repo.Purchase,[new(){Id=1,Name="Bodega"}],Snapshot(true).Importation);Assert.False(receipt.CanEditCosts);Assert.True(receipt.CostsComplete);Assert.Equal("1",receipt.Rate);
    }
    [Fact]public async Task LostResponseRetainsImportRequestAndRecoversWithoutSecondWrite()
    {
        var repo=new MvpUiRepository{Importation=Snapshot(),LoseResponse=true};var session=Session();var dialogs=new Dialogs();
        var vm=new PurchasesViewModel(new(repo,session),dialogs,session,new(repo,session),NullLogger<PurchasesViewModel>.Instance,new(repo,session));
        await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Items.Single();await vm.OpenCommand.ExecuteAsync(null);
        dialogs.Action=async f=>{f.Fields.Single(x=>x.Key=="reason").Value="Evento de prueba";f.Confirmed=true;await((IAsyncRelayCommand)f.SaveCommand).ExecuteAsync(null);};
        await vm.ImportEventCommand.ExecuteAsync(null);Assert.True(vm.Operation.HasPending);var request=Assert.IsType<ImportEvent>(Assert.Single(repo.Writes));
        Assert.Equal(repo.Importation.Importation.RowVersion,request.ExpectedVersion);repo.FoundReceipt=new(301,request.OperationId);await vm.Operation.CheckCommand.ExecuteAsync(null);
        Assert.False(vm.Operation.HasPending);Assert.Single(repo.Writes);
    }
    [Fact]public async Task OldSessionOrSelectionCannotPublishImportSnapshot()
    {
        var repo=new MvpUiRepository();var session=Session();var vm=new PurchasesViewModel(new(repo,session),new Dialogs(),session,new(repo,session),NullLogger<PurchasesViewModel>.Instance,new(repo,session));
        await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Items.Single();var pending=new TaskCompletionSource<ImportationSnapshot?>();repo.ImportReadTask=pending.Task;
        var read=vm.OpenCommand.ExecuteAsync(null);session.Clear();pending.SetResult(Snapshot());await read;Assert.Null(vm.Importation);Assert.Null(vm.Detail);
    }
    private sealed class Dialogs:IMvpDialogService
    {public Func<MvpFormModel,Task>? Action;public async Task<bool> ShowAsync(MvpFormModel f){if(Action is not null)await Action(f);return true;}}
}
