using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;
public sealed class CustomerUiTests
{
    [Fact]
    public async Task NewCustomerHasAuditReasonAndSelectedCatalogTypeCanBeSaved()
    {
        CustomerChange? saved = null;
        var form = new CustomerEditorDialogModel(CustomerEditorKind.General, new() { Name = "Clínica de prueba" }, null, null, null,
            new([new() { Id = 1, Name = "Paciente" }, new() { Id = 2, Name = "Clínica" }], []), model =>
            {
                var editor = (CustomerEditorDialogModel)model;
                saved = new(editor.BuildCustomer(), editor.Reason);
                return Task.FromResult(CustomerValidator.Validate(saved) is null);
            });
        form.SelectedTypeId = 2;
        form.Confirmed = true;
        await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);
        Assert.NotNull(saved);
        Assert.Equal((short)2, saved.Customer.TypeId);
        Assert.Equal("Alta de cliente", saved.Reason);
        Assert.Null(CustomerValidator.Validate(saved));
    }

    [Fact]
    public void EditingCustomerStillRequiresExplicitAuditReason()
    {
        var form = new CustomerEditorDialogModel(CustomerEditorKind.General,
            new() { Id = 1, Code = "CLI-1", Name = "Clínica", TypeId = 1, RowVersion = new byte[8] }, null, null, null,
            new([new() { Id = 1, Name = "Clínica" }], []), _ => Task.FromResult(false));
        Assert.Empty(form.Reason);
        Assert.NotNull(CustomerValidator.Validate(new CustomerChange(form.BuildCustomer(), form.Reason)));
    }
    private static UserSession Session(params string[] permissions)
    {
        var session = new UserSession(); session.Begin(new(1, "prueba", "Prueba", 1, [], permissions, false)); return session;
    }
    private static CustomersViewModel ViewModel(CustomerWorkflowRepository repo, UserSession session, CustomerTestDialogs dialogs) => new(new(repo, session), session, dialogs, NullLogger<CustomersViewModel>.Instance);
    private static async Task Open(CustomersViewModel vm) { await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.First(); await vm.OpenCommand.ExecuteAsync(null); }
    [Fact]
    public async Task ReturnToListPreservesCustomerAndFiltersAndSessionClearsDetail()
    {
        var session = Session("CLIENTES.VER");
        var vm = ViewModel(new(), session, new());
        await Open(vm);
        var selected = vm.Selected;
        vm.Search = "Cliente";
        Assert.True(vm.IsDetailOpen);
        vm.ReturnToListCommand.Execute(null);
        Assert.False(vm.IsDetailOpen);
        Assert.Same(selected, vm.Selected);
        Assert.Equal("Cliente", vm.Search);
        await vm.OpenCommand.ExecuteAsync(null);
        Assert.True(vm.IsDetailOpen);
        vm.Selected = null;
        await vm.RefreshDetailCommand.ExecuteAsync(null);
        Assert.True(vm.IsDetailOpen);
        Assert.NotNull(vm.Detail);
        session.Clear();
        Assert.False(vm.IsDetailOpen);
        Assert.Null(vm.Detail);
    }
    [Fact]
    public async Task RowLinkOpensItsCustomerAndStillRequiresReadPermission()
    {
        var repo = new CustomerWorkflowRepository();
        var session = Session("CLIENTES.VER");
        var vm = ViewModel(repo, session, new());
        await vm.ReloadCommand.ExecuteAsync(null);
        Assert.Null(vm.Selected);
        await vm.OpenCustomerCommand.ExecuteAsync(vm.Items.Single());
        Assert.True(vm.IsDetailOpen);
        Assert.Same(vm.Items.Single(), vm.Selected);
        Assert.Equal(repo.Customer.Id, vm.Detail!.Id);
        session.Clear();
        await vm.OpenCustomerCommand.ExecuteAsync(repo.Customer);
        Assert.Null(vm.Detail);
        Assert.False(vm.IsDetailOpen);
    }

    [Fact]
    public async Task CreatePermissionDoesNotGrantEditOrCreditConfiguration()
    {
        var repo = new CustomerWorkflowRepository(); var service = new CustomerService(repo, Session("CLIENTES.VER", "CLIENTES.CREAR"));
        Assert.True(service.CanCreate); Assert.False(service.CanEdit); Assert.False(service.CanConfigureCredit);
        Assert.True((await service.SaveAsync(new(new() { Code = " CLI ", Name = " Cliente ", TypeId = 1, Nit = " ", Email = " prueba@example.test " }, " Alta "))).IsSuccess);
        Assert.Equal("CLI", repo.LastCustomer!.Customer.Code); Assert.Null(repo.LastCustomer.Customer.Nit); Assert.Equal("prueba@example.test", repo.LastCustomer.Customer.Email);
        Assert.Equal("FORBIDDEN", (await service.SaveAsync(new(repo.Customer, "Edición"))).ErrorCode);
        Assert.Equal("FORBIDDEN", (await service.SaveCreditAsync(new(1, new byte[8], new(), "Crédito"))).ErrorCode);
    }
    [Fact]
    public async Task CreditPermissionIsIndependentAndGeneralEditCannotChangeCredit()
    {
        var repo = new CustomerWorkflowRepository(); var creditService = new CustomerService(repo, Session("CLIENTES.VER", "CLIENTES.CREDITO_CONFIGURAR"));
        Assert.False(creditService.CanEdit); Assert.True(creditService.CanConfigureCredit);
        Assert.True((await creditService.SaveCreditAsync(new(1, new byte[8], new() { Enabled = true, Limit = 250.15m }, "Configurar"))).IsSuccess);
        var service = new CustomerService(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"));
        Assert.True((await service.SaveAsync(new(repo.Customer with { Phone = "123" }, "Contacto"))).IsSuccess);
        Assert.Equal(250.15m, repo.Credit.Limit);
    }
    [Theory]
    [InlineData("FISCAL")]
    [InlineData("ENTREGA")]
    [InlineData("OTRA")]
    public async Task AddressTypesArePreservedAndOptionalFieldsNormalize(string type)
    {
        var repo = new CustomerWorkflowRepository(); var service = new CustomerService(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"));
        Assert.True((await service.SaveAddressAsync(new(1, new byte[8], new() { Type = type, Address = " Calle de prueba ", Reference = " " }, "Dirección"))).IsSuccess);
        Assert.Equal(type, repo.LastAddress!.Address.Type); Assert.Equal("Calle de prueba", repo.LastAddress.Address.Address); Assert.Null(repo.LastAddress.Address.Reference);
    }
    [Fact]
    public void ValidatorsEnforceSchemaAndPreserveNullableCreditMeaning()
    {
        Assert.NotNull(CustomerValidator.Validate(new CustomerAddressChange(1, new byte[8], new() { Type = "ENVIO", Address = "Prueba" }, "Motivo")));
        Assert.NotNull(CustomerValidator.Validate(new CustomerCreditChange(1, new byte[8], new() { Limit = 1.001m }, "Motivo")));
        Assert.NotNull(CustomerValidator.Validate(new CustomerCreditChange(1, new byte[8], new() { Blocked = true }, "Motivo")));
        Assert.NotNull(CustomerValidator.Validate(new CustomerContactChange(1, [], new() { Name = "Prueba" }, "Motivo")));
        Assert.Null(CustomerValidator.Validate(new CustomerCreditChange(1, new byte[8], new() { Limit = null, Days = null, NoticeDays = null }, "Motivo")));
    }
    [Fact]
    public async Task HistoriesRequireSalesPermissionAndRejectUnboundedPaging()
    {
        var repo = new CustomerWorkflowRepository(); var service = new CustomerService(repo, Session("CLIENTES.VER"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SalesAsync(new(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AccountsAsync(new(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PaymentsAsync(new(1)));
        var authorized = new CustomerService(repo, Session("CLIENTES.VER", "VENTAS.VER"));
        await Assert.ThrowsAsync<ArgumentException>(() => authorized.SalesAsync(new(1, PageSize: 101)));
        await Assert.ThrowsAsync<ArgumentException>(() => authorized.SalesAsync(new(1, int.MaxValue)));
    }
    [Fact]
    public async Task CancellingTheEditorDoesNotMutateTheReadModel()
    {
        var repo = new CustomerWorkflowRepository(); var dialogs = new CustomerTestDialogs(); var vm = ViewModel(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"), dialogs); await Open(vm);
        dialogs.Edit = model => { model.Customer.Name = "Descartado"; model.Customer.Active = false; return Task.FromResult(false); };
        await vm.EditCustomerCommand.ExecuteAsync(null);
        Assert.Equal("Cliente de prueba", vm.Detail!.Name); Assert.True(vm.Detail.Active); Assert.Equal(0, repo.Writes);
    }
    [Fact]
    public async Task SavingAddressReloadsVersionAndDoesNotReplaceContacts()
    {
        var repo = new CustomerWorkflowRepository(); var dialogs = new CustomerTestDialogs(); var vm = ViewModel(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"), dialogs); await Open(vm);
        dialogs.Edit = async model => { model.Address.Address = "Otra dirección"; model.Reason = "Nueva dirección"; model.Confirmed = true; await ((IAsyncRelayCommand)model.SaveCommand).ExecuteAsync(null); return true; };
        await vm.NewAddressCommand.ExecuteAsync(null);
        Assert.NotNull(repo.LastAddress); Assert.Single(vm.Contacts); Assert.Equal(repo.Customer.RowVersion, vm.Detail!.RowVersion);
        await vm.NewAddressCommand.ExecuteAsync(null); Assert.Equal(BitConverter.GetBytes(1L), repo.LastAddress!.ExpectedVersion);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictOrLostResponsePreventsRepeatedWrites(bool loseResponse)
    {
        var repo = new CustomerWorkflowRepository { Conflict = !loseResponse, LoseResponse = loseResponse }; var dialogs = new CustomerTestDialogs(); var vm = ViewModel(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"), dialogs); await Open(vm);
        dialogs.Edit = async model =>
        {
            model.Reason = "Cambio de cliente"; model.Confirmed = true;
            await ((IAsyncRelayCommand)model.SaveCommand).ExecuteAsync(null); Assert.False(model.CanSubmit);
            await ((IAsyncRelayCommand)model.SaveCommand).ExecuteAsync(null); return false;
        };
        await vm.EditCustomerCommand.ExecuteAsync(null); Assert.Equal(1, repo.Writes);
    }
    [Fact]
    public async Task InactiveClientBlocksChildrenButAllowsGeneralCorrection()
    {
        var repo = new CustomerWorkflowRepository(); var dialogs = new CustomerTestDialogs(); var vm = ViewModel(repo, Session("CLIENTES.VER", "CLIENTES.EDITAR"), dialogs); await Open(vm);
        vm.Detail!.Active = false; Assert.True(vm.CanEdit); Assert.False(vm.CanEditChildren); await vm.NewAddressCommand.ExecuteAsync(null); Assert.Equal(0, dialogs.Opened);
    }
    [Fact]
    public async Task LateReadCannotRepopulateAnotherSession()
    {
        var repo = new CustomerWorkflowRepository(); var dialogs = new CustomerTestDialogs(); var session = Session("CLIENTES.VER"); var vm = ViewModel(repo, session, dialogs);
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.First(); repo.PendingRead = new();
        var read = vm.OpenCommand.ExecuteAsync(null); session.Clear(); repo.PendingRead.SetResult(new(repo.Customer, repo.Credit, repo.Contacts, repo.Addresses)); await read;
        Assert.Null(vm.Detail); Assert.Empty(vm.Contacts); Assert.Empty(vm.Addresses); Assert.False(vm.CanRead);
    }
    [Fact]
    public async Task ReadRevocationClearsDataAndSession()
    {
        var repo = new CustomerWorkflowRepository(); var session = Session("CLIENTES.VER"); var vm = ViewModel(repo, session, new()); await Open(vm);
        repo.DenyRead = true; await vm.OpenCommand.ExecuteAsync(null); Assert.False(session.IsAuthenticated); Assert.Null(vm.Credit);
    }
    [Fact]
    public async Task HistoryPaginationIsBoundedAndFinancialDataLoadsFromService()
    {
        var repo = new CustomerWorkflowRepository(); var vm = ViewModel(repo, Session("CLIENTES.VER", "VENTAS.VER"), new()); await Open(vm);
        Assert.False(vm.HasLoadedSales); Assert.False(vm.HasLoadedAccounts); Assert.False(vm.HasLoadedPayments);
        await vm.LoadSalesCommand.ExecuteAsync(null); Assert.True(vm.HasLoadedSales); Assert.Single(vm.Sales); await vm.NextSalesCommand.ExecuteAsync(null); Assert.Equal(2, repo.LastHistory!.Page);
        await vm.NextSalesCommand.ExecuteAsync(null); Assert.Equal(2, repo.LastHistory.Page);
        await vm.LoadAccountsCommand.ExecuteAsync(null); Assert.Equal(90, vm.Balance!.Available); Assert.Single(vm.Accounts); Assert.True(vm.HasLoadedAccounts);
        await vm.LoadPaymentsCommand.ExecuteAsync(null); Assert.Single(vm.Payments); Assert.True(vm.HasLoadedPayments);
        await vm.RefreshDetailCommand.ExecuteAsync(null);
        Assert.False(vm.HasLoadedSales); Assert.False(vm.HasLoadedAccounts); Assert.False(vm.HasLoadedPayments); Assert.Null(vm.Balance);
    }
    [Fact]
    public void CreditEditorUsesNullableValuesAndRejectsThousandsSeparators()
    {
        var model = new CustomerEditorDialogModel(CustomerEditorKind.Credit, new(), null, null, new() { Limit = null, Days = null, NoticeDays = null }, new([], []), _ => Task.FromResult(false));
        Assert.Null(model.BuildCredit()!.Limit); model.Limit = "1,000"; Assert.Null(model.BuildCredit());
        model.Limit = "10.25"; Assert.Equal(10.25m, model.BuildCredit()!.Limit);
    }
}
