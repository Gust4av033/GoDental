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
public sealed class PurchaseTests
{
    private static UserSession Session(params string[] permissions)
    {
        var s = new UserSession(); s.Begin(new(1, "test", "Prueba", 1, [], permissions, false, "test|purchases")); return s;
    }
    [Theory]
    [InlineData("BORRADOR", "CONFIRMADA", false, true)] [InlineData("ENVIADA", "CONFIRMADA", false, true)]
    [InlineData("CONFIRMADA", "RECIBIDA", false, false)] [InlineData("RECIBIDA_PARCIAL", "CANCELADA", true, false)]
    [InlineData("CONFIRMADA", "CANCELADA", false, true)] [InlineData("BORRADOR", "DESPACHADA", false, false)]
    [InlineData("CANCELADA", "CONFIRMADA", false, false)]
    public void StatesCannotSimulateReceivingOrCancelStock(string state, string next, bool received, bool valid) => Assert.Equal(valid, PurchaseRules.CanTransition(state, next, received));
    [Fact] public void CostsUseActualRateAndSixDecimalsWithoutRewritingHistory()
    {
        Assert.Equal(1.993333m, PurchaseRules.PurchaseCostUsd(10, 1, 30, .2m));
        Assert.Equal(13.333333m, PurchaseRules.Average(10, 10, 5, 20));
        Assert.Equal(20, PurchaseRules.Average(0, 7, 5, 20));
        Assert.Equal(1.01m, PurchaseRules.Money(1.005m));
    }
    [Fact] public void FrozenReceiptIdentityMatchesNormalizationAndAllVersionsMatter()
    {
        var r = new PreparePurchaseReceipt(Guid.NewGuid(), 1, new byte[8], 1, 1, true, [new(2, 1, LotNumber: " B "), new(1, 1, LotNumber: "A")], " Test ");
        var frozen = PurchaseValidator.Freeze(r);
        Assert.Equal(OperationIdentity.Describe(r), OperationIdentity.Describe(frozen));
        Assert.NotEqual(OperationIdentity.Describe(r), OperationIdentity.Describe(r with { CostsComplete = false }));
        Assert.NotEqual(OperationIdentity.Describe(r), OperationIdentity.Describe(r with { ExchangeRate = 2 }));
        r.ExpectedOrderVersion[0] = 9; Assert.Equal(0, frozen.ExpectedOrderVersion[0]);
        Assert.True(OperationIdentity.IsValid(OperationIdentity.Describe(frozen)));
    }
    [Fact] public async Task WritesAndReadsRequirePurchasePermissions()
    {
        var repo = new MvpUiRepository(); var session = Session("COMPRAS.VER"); var service = new PurchaseService(repo, session);
        var request = new PurchaseTransition(Guid.NewGuid(), 1, new byte[8], "CONFIRMADA", "Prueba");
        Assert.Equal("FORBIDDEN", (await service.TransitionAsync(request)).ErrorCode); Assert.Empty(repo.Writes);
        session.Clear(); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CatalogsAsync());
    }
    [Fact] public async Task InvalidOrderDoesNotReachRepository()
    {
        var repo = new MvpUiRepository(); var service = new PurchaseService(repo, Session("COMPRAS.VER", "COMPRAS.GESTIONAR"));
        var r = new SavePurchaseOrder(Guid.NewGuid(), 0, [], 1, 1, 1, DateTime.Today, [new(1, 1, 10), new(1, 2, 12)], "Duplicado");
        Assert.Equal("VALIDATION", (await service.SaveAsync(r)).ErrorCode);
        Assert.Equal("VALIDATION", (await service.SaveAsync(r with { Lines = [new(1, 1, 10, 11)] })).ErrorCode);
        Assert.Empty(repo.Writes);
    }
    [Fact] public async Task LateReadCannotExposeOldPurchaseToNextSession()
    {
        var repo = new MvpUiRepository(); var completion = new TaskCompletionSource<PurchaseOrderSnapshot?>(); repo.PurchaseReadTask = completion.Task;
        var session = Session("COMPRAS.VER", "COMPRAS.GESTIONAR"); var vm = Vm(repo, session, new());
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.Single(); var read = vm.OpenCommand.ExecuteAsync(null);
        session.Clear(); completion.SetResult(repo.Purchase); await read;
        Assert.Null(vm.Detail); Assert.False(vm.IsDetailOpen); Assert.Empty(vm.Items); Assert.False(vm.CanManage);
    }
    [Fact] public async Task ReceiptKeepsExactRequestAndRecoversWithoutAnotherStockWrite()
    {
        var repo = new MvpUiRepository { LoseResponse = true }; var session = Session("COMPRAS.VER", "COMPRAS.GESTIONAR"); var dialogs = new Dialogs(); var vm = Vm(repo, session, dialogs);
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.Single(); await vm.OpenCommand.ExecuteAsync(null);
        dialogs.Action = async f =>
        {
            var draft = Assert.IsType<PurchaseReceiptDraftModel>(f.Body); draft.Quantity = "3"; draft.CostsComplete = true; draft.AddCommand.Execute(null);
            f.Fields.Single(x => x.Key == "reason").Value = "Recepción de prueba"; f.Confirmed = true; await ((IAsyncRelayCommand)f.SaveCommand).ExecuteAsync(null);
            draft.Quantity = "9";
        };
        await vm.PrepareCommand.ExecuteAsync(null);
        var request = Assert.IsType<PreparePurchaseReceipt>(Assert.Single(repo.Writes));
        Assert.Equal(3, request.Lines.Single().Quantity); Assert.True(request.CostsComplete); Assert.Equal(repo.Purchase.Order.RowVersion, request.ExpectedOrderVersion);
        Assert.True(vm.Operation.HasPending); Assert.False(vm.CanPrepare);
        repo.FoundReceipt = new(201, request.OperationId); await vm.Operation.CheckCommand.ExecuteAsync(null);
        Assert.False(vm.Operation.HasPending); Assert.Single(repo.Writes); Assert.Equal(201, vm.ReceiptDetail!.Receipt.Id);
    }
    [Fact] public async Task ApplyingReceiptUsesBothVersionsAndRequiresExplicitConfirmation()
    {
        var repo = new MvpUiRepository(); var session = Session("COMPRAS.VER", "COMPRAS.GESTIONAR"); var dialogs = new Dialogs(); var vm = Vm(repo, session, dialogs);
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.Single(); await vm.OpenCommand.ExecuteAsync(null);
        vm.SelectedReceipt = vm.Detail!.Receipts.Single(); await vm.ReadReceiptCommand.ExecuteAsync(null);
        dialogs.Action = async f =>
        {
            await ((IAsyncRelayCommand)f.SaveCommand).ExecuteAsync(null); Assert.Empty(repo.Writes);
            f.Fields.Single(x => x.Key == "reason").Value = "Aplicación de prueba"; f.Confirmed = true; await ((IAsyncRelayCommand)f.SaveCommand).ExecuteAsync(null);
        };
        await vm.ApplyCommand.ExecuteAsync(null);
        var r = Assert.IsType<PurchaseReceiptAction>(Assert.Single(repo.Writes)); Assert.True(r.Apply);
        Assert.Equal(repo.Purchase.Order.RowVersion, r.ExpectedOrderVersion); Assert.Equal(repo.PurchaseReceipt.Receipt.RowVersion, r.ExpectedVersion);
    }
    [Fact] public void MultiLotCaptureTotalsCannotExceedPending()
    {
        var repo = new MvpUiRepository(); var draft = new PurchaseReceiptDraftModel(repo.Purchase, [new() { Id = 1, Name = "Almacén" }]);
        draft.Quantity = "6"; draft.AddCommand.Execute(null); draft.Quantity = "5"; draft.AddCommand.Execute(null);
        Assert.Single(draft.Items); Assert.Contains("pendiente", draft.StatusMessage); Assert.False(draft.CostsComplete);
    }
    private static PurchasesViewModel Vm(MvpUiRepository repo, UserSession session, Dialogs dialogs) => new(new(repo, session), dialogs, session, new(repo, session), NullLogger<PurchasesViewModel>.Instance);
    private sealed class Dialogs : IMvpDialogService
    {
        public Func<MvpFormModel, Task>? Action { get; set; }
        public async Task<bool> ShowAsync(MvpFormModel form) { if (Action is not null) await Action(form); return true; }
    }
}
