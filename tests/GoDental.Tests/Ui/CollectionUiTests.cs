using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class CollectionUiTests
{
    [Fact] public async Task PreviewSurvivesCancelButCannotPrintAfterChangingSizeOrSession()
    {
        var session=new UserSession();session.Begin(new(1,"test","Prueba",1,[],["CLIENTES.VER","VENTAS.VER","VENTAS.REIMPRIMIR"],false));
        var repo=new MvpUiRepository();var collection=new CollectionUiRepository();var files=new Files();var vm=new CreditViewModel(new(repo,session),new(repo,session,new Identity()),new(new CustomerWorkflowRepository(),session),new Dialogs(),session,new(repo,session),NullLogger<CreditViewModel>.Instance,new(collection,new Renderer(),session),files,files);
        await vm.ReloadCommand.ExecuteAsync(null);await vm.OpenAccountCommand.ExecuteAsync(vm.Items.Single());
        await vm.PreviewStatementCommand.ExecuteAsync(null);Assert.True(vm.CanUsePreview);Assert.Equal(4,vm.SelectedDetailTab);
        var preview=vm.ReceiptPreview;await vm.ExportCommand.ExecuteAsync(null);await vm.PrintReceiptCommand.ExecuteAsync(null);
        Assert.Same(preview!.Pdf,files.Pdf);Assert.Same(preview,files.Document);Assert.Same(preview,vm.ReceiptPreview);
        vm.PrintSettings.Paper="Ticket 80 mm";Assert.False(vm.CanUsePreview);files.Pdf=null;await vm.ExportCommand.ExecuteAsync(null);Assert.Null(files.Pdf);
        await vm.PreviewStatementCommand.ExecuteAsync(null);Assert.True(vm.CanUsePreview);
        session.Clear();Assert.Null(vm.ReceiptPreview);Assert.Empty(vm.Notices);Assert.Null(vm.Statement);Assert.False(vm.CanUsePreview);
    }
    [Fact] public async Task LateStatementDoesNotReplaceNewlySelectedCustomer()
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["CLIENTES.VER","VENTAS.VER"],false));var r=new MvpUiRepository();var c=new CollectionUiRepository();var vm=new CreditViewModel(new(r,s),new(r,s,new Identity()),new(new CustomerWorkflowRepository(),s),new Dialogs(),s,new(r,s),NullLogger<CreditViewModel>.Instance,new(c,new Renderer(),s));
        await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Items.Single();var pending=new TaskCompletionSource<CustomerStatement?>(TaskCreationOptions.RunContinuationsAsynchronously);c.StatementRead=pending.Task;
        var read=vm.LoadStatementCommand.ExecuteAsync(null);vm.Selected=new(){Id=2,CustomerId=2};pending.SetResult(c.Statement);await read;Assert.Null(vm.Statement);
    }
    [Fact] public async Task NoticeLostResponseKeepsTheSameGuidAndHumanReadableChoices()
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],["CLIENTES.VER","VENTAS.VER","CLIENTES.COBRANZA_GESTIONAR"],false));
        var r=new MvpUiRepository();r.Account.NoticeDue=true;var c=new CollectionUiRepository{LoseResponse=true};var dialogs=new Dialogs();
        var vm=new CreditViewModel(new(r,s),new(r,s,new Identity()),new(new CustomerWorkflowRepository(),s),dialogs,s,new(r,s),NullLogger<CreditViewModel>.Instance,new(c,new Renderer(),s));
        await vm.ReloadCommand.ExecuteAsync(null);vm.Selected=vm.Items.Single();
        dialogs.Action=async form=>
        {
            Assert.Equal("Revisión interna",form.Choice<CreditViewModel.NoticeChoice>("medium").Name);
            form.Fields.Single(f=>f.Key=="reason").Value="Gestión revisada";form.Confirmed=true;
            await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);
        };
        await vm.RegisterNoticeCommand.ExecuteAsync(null);Assert.True(vm.Operation.HasPending);Assert.Single(c.Writes);var first=c.Writes[0];
        c.LoseResponse=false;await vm.Operation.RetryCommand.ExecuteAsync(null);Assert.False(vm.Operation.HasPending);Assert.Equal(2,c.Writes.Count);
        Assert.Equal(first.OperationId,c.Writes[1].OperationId);Assert.Equal("INTERNO",first.Medium);Assert.Equal("GESTION_INTERNA",first.Outcome);Assert.Single(vm.Notices);
    }
    private sealed class Renderer:ICreditDocumentRenderer
    {
        public ReceiptDocument RenderStatement(CustomerStatement s,PrintLayout l)=>new([1,2],[[1]],l.WidthMm,l.HeightMm,l.MarginMm);
        public ReceiptDocument RenderPayment(PaymentReceiptData p,PrintLayout l)=>new([3,4],[[1]],l.WidthMm,l.HeightMm,l.MarginMm);
    }
    private sealed class Files:IPdfFileService,IPdfPrintService
    {public byte[]? Pdf;public ReceiptDocument? Document;public Task<bool> SaveAsync(byte[] p,string n){Pdf=p;return Task.FromResult(false);}public Task<bool> PrintAsync(ReceiptDocument d,string n){Document=d;return Task.FromResult(false);}}
    private sealed class Dialogs:IMvpDialogService { public Func<MvpFormModel,Task>? Action; public async Task<bool> ShowAsync(MvpFormModel model){if(Action is not null)await Action(model);return false;} }
    private sealed class Identity:GoDental.Application.Abstractions.Security.IIdentityGateway
    {
        public Task<GoDental.Application.Common.Result<UserIdentity>> SignInAsync(string u,string p,string machine,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<bool> NeedsAdministratorAsync(CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<GoDental.Application.Common.Result<UserIdentity>> CreateAdministratorAsync(string u,string n,string p,string machine,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<GoDental.Application.Common.Result> ChangePasswordAsync(int id,string old,string pass,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task SignOutAsync(int id,int? station,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
