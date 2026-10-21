using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;

public sealed class ComplementsTests
{
    private static UserSession Session(params string[] permissions)
    {
        var session = new UserSession(); session.Begin(new(1, "test", "Cuenta de prueba", 1, [], permissions, false, "test|complements")); return session;
    }
    [Theory]
    [InlineData(-1, 35)] [InlineData(40, 35)] [InlineData(7, 101)] [InlineData(7.12345, 35)]
    public async Task InvalidThresholdsNeverReachPersistence(decimal justification, decimal authorization)
    {
        var repo = new MvpUiRepository(); var service = new CommercialParametersService(repo, Session("AJUSTES.EDITAR"));
        var result = await service.SaveAsync(new(Guid.NewGuid(), justification, authorization, new byte[8], new byte[8], "Cambio"));
        Assert.Equal("VALIDATION", result.ErrorCode); Assert.Empty(repo.Writes);
    }
    [Fact] public async Task ParametersRequirePermissionAndDiscardLateSessionRead()
    {
        var repo = new MvpUiRepository(); var session = Session(); var service = new CommercialParametersService(repo, session);
        Assert.Equal("FORBIDDEN", (await service.SaveAsync(new(Guid.NewGuid(), 8, 40, new byte[8], new byte[8], "Cambio"))).ErrorCode);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync());
        var completion = new TaskCompletionSource<CommercialParameters>(); repo.ParametersTask = completion.Task;
        session.Begin(new(1, "test", "Cuenta", 1, [], ["AJUSTES.EDITAR"], false, "test|complements"));
        var vm = new CommercialParametersViewModel(service, new Dialogs(), session, new(repo, session), NullLogger<CommercialParametersViewModel>.Instance);
        var loading = vm.ReloadCommand.ExecuteAsync(null); session.Clear(); completion.SetResult(repo.Parameters); await loading;
        Assert.Null(vm.Parameters); Assert.False(vm.CanChange); Assert.Empty(repo.Writes);
    }
    [Fact] public async Task ParameterLostResponsePreservesVersionValuesAndTicket()
    {
        var repo = new MvpUiRepository { LoseResponse = true }; var session = Session("AJUSTES.EDITAR"); var dialogs = new Dialogs();
        var vm = new CommercialParametersViewModel(new(repo, session), dialogs, session, new(repo, session), NullLogger<CommercialParametersViewModel>.Instance);
        await vm.ReloadCommand.ExecuteAsync(null);
        dialogs.Action = async f => { Set(f, "justification", "8"); Set(f, "authorization", "40"); Set(f, "reason", "Revisión comercial"); await Save(f); };
        await vm.ChangeCommand.ExecuteAsync(null);
        var request = Assert.IsType<ChangeCommercialParameters>(Assert.Single(repo.Writes));
        Assert.Equal(8, request.Justification); Assert.Equal(repo.Parameters.JustificationVersion, request.JustificationVersion);
        Assert.True(vm.Operation.HasPending); Assert.False(vm.CanChange);
        Assert.Equal(OperationIdentity.Describe(request), OperationIdentity.Describe(request with { Reason = " " + request.Reason + " " }));
        repo.FoundReceipt = new(50, request.OperationId); await vm.Operation.CheckCommand.ExecuteAsync(null);
        Assert.False(vm.Operation.HasPending); Assert.Single(repo.Writes);
    }
    [Theory]
    [InlineData("4006381333931", "EAN13", true)] [InlineData("4006381333932", "EAN13", false)]
    [InlineData("036000291452", "UPC", true)] [InlineData("036000291453", "UPC", false)]
    [InlineData("", "CODE128", true)] [InlineData("", "EAN13", false)] [InlineData("ABC\n123", "CODE128", false)]
    public void ExternalBarcodesCheckLengthAndCheckDigit(string code, string type, bool valid)
        => Assert.Equal(valid, BarcodeValidator.Validate(code, type) is null);
    [Fact] public async Task BarcodeToggleUsesFreshProductVersionAndKeepsCode()
    {
        var repo = new MvpUiRepository(); repo.Barcodes.Add(new() { Id = 4, Code = "GD0000000001", Type = "CODE128", Active = true, Generated = true });
        var session = Session("INVENTARIO.VER", "INVENTARIO.PRODUCTOS_GESTIONAR"); var dialogs = new Dialogs();
        var vm = new BarcodesViewModel(new(repo, session), dialogs, session, new(repo, session), NullLogger<BarcodesViewModel>.Instance);
        await vm.OpenAsync(new() { Id = 1, Sku = "PRD", Name = "Prueba" }); vm.Selected = vm.Items.Single();
        dialogs.Action = async f => { Set(f, "reason", "Código desactivado"); await Save(f); };
        await vm.ToggleCommand.ExecuteAsync(null);
        var request = Assert.IsType<BarcodeChange>(Assert.Single(repo.Writes));
        Assert.False(request.Active); Assert.Equal("GD0000000001", request.Code); Assert.Equal(new byte[] {1,2,3,4,5,6,7,8}, request.ExpectedVersion);
        session.Clear(); Assert.Empty(vm.Items); Assert.False(vm.CanManage);
    }
    [Fact] public async Task LabelPreviewInvalidatesChangedSizeAndSessionWithoutBusinessWrites()
    {
        var repo = new MvpUiRepository(); repo.Barcodes.Add(new() { Id = 4, Code = "GD0000000001", Type = "CODE128", Active = true });
        var session = Session("INVENTARIO.VER"); var files = new LabelFiles();
        var renderer = new GoDental.Infrastructure.Operations.BarcodeLabelPdfRenderer(Microsoft.Extensions.Options.Options.Create(new GoDental.Infrastructure.Operations.ReceiptPdfOptions()));
        var vm = new BarcodesViewModel(new(repo, session), new Dialogs(), session, new(repo, session), NullLogger<BarcodesViewModel>.Instance,
            new BarcodeLabelService(repo, renderer, session), files, files);
        await vm.OpenAsync(new() { Id = 1, Sku = "PRD", Name = "Ejemplo" }); vm.Selected = vm.Items.Single();
        await vm.PreviewReceiptCommand.ExecuteAsync(null); Assert.True(vm.CanUsePreview); var doc = vm.ReceiptPreview!;
        await vm.ExportCommand.ExecuteAsync(null); await vm.PrintReceiptCommand.ExecuteAsync(null);
        Assert.Same(doc.Pdf, files.Bytes); Assert.Same(doc, files.Document);
        vm.PrintSettings.Width = "60"; Assert.False(vm.CanUsePreview); files.Bytes = null; files.Document = null;
        await vm.ExportCommand.ExecuteAsync(null); await vm.PrintReceiptCommand.ExecuteAsync(null); Assert.Null(files.Bytes); Assert.Null(files.Document);
        session.Clear(); Assert.Null(vm.ReceiptPreview); Assert.Empty(repo.Writes);
    }
    private sealed class LabelFiles : IPdfFileService, IPdfPrintService
    {
        public byte[]? Bytes; public ReceiptDocument? Document;
        public Task<bool> SaveAsync(byte[] pdf, string suggestedName) { Bytes = pdf; return Task.FromResult(true); }
        public Task<bool> PrintAsync(ReceiptDocument document, string name) { Document = document; return Task.FromResult(true); }
    }
    [Fact] public void CardCheckoutRequiresReferenceAndKeepsManualNotice()
    {
        var repo = new MvpUiRepository(); var vm = new CheckoutModel(repo.QuoteFactory(new(1, null, [new(1, 1)])), repo.Methods);
        vm.Method = vm.Methods.Single(m => m.Code == "TARJETA"); vm.Amount = "25"; vm.AddCommand.Execute(null);
        Assert.Empty(vm.Payments); Assert.Contains("referencia", vm.StatusMessage);
        vm.Reference = "COMPROBANTE-EXTERNO-PRUEBA"; vm.AddCommand.Execute(null);
        var payment = Assert.Single(vm.Snapshot()); Assert.Equal(vm.Method.Id, payment.MethodId); Assert.Equal(vm.Reference, payment.Reference);
        Assert.True(vm.HasManualCard); Assert.Contains("no procesa ni verifica", vm.CardNotice);
        Assert.Equal(0, vm.Tendered);
    }
    private static void Set(MvpFormModel f, string key, string value) => f.Fields.Single(x => x.Key == key).Value = value;
    private static Task Save(MvpFormModel f) { f.Confirmed = true; return ((IAsyncRelayCommand)f.SaveCommand).ExecuteAsync(null); }
    private sealed class Dialogs : IMvpDialogService
    {
        public Func<MvpFormModel, Task>? Action { get; set; }
        public async Task<bool> ShowAsync(MvpFormModel form) { if (Action is not null) await Action(form); return true; }
    }
}
