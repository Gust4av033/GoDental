using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;

public sealed class MvpUiTests
{
    [Fact]
    public async Task LostResponseRetainsExactRequestAndIdAndDoesNotAutomaticallyRetry()
    {
        var c = new Context(); var operation = c.Operation(); var id = Guid.NewGuid();
        var request = new OpenCashShift(id, 1, 10, "Apertura de prueba"); c.Repository.LoseResponse = true;
        await operation.StartAsync(request, id, q => c.Cash.OpenAsync(q), _ => Task.CompletedTask);
        Assert.True(operation.HasPending); Assert.Same(request, operation.Request); Assert.Single(c.Repository.Writes);
        await operation.CheckCommand.ExecuteAsync(null);
        Assert.True(operation.HasPending); Assert.Single(c.Repository.Writes);
        c.Repository.LoseResponse = false;
        await operation.RetryCommand.ExecuteAsync(null);
        Assert.False(operation.HasPending); Assert.Equal(2, c.Repository.Writes.Count); Assert.All(c.Repository.Writes, q => Assert.Equal(request, q));
    }
    [Fact]
    public async Task OperationLookupCompletesWithoutSecondWrite()
    {
        var c = new Context(); var operation = c.Operation(); var id = Guid.NewGuid(); c.Repository.LoseResponse = true;
        var completed = 0;
        await operation.StartAsync(new OpenCashShift(id, 1, 10, "Apertura"), id, q => c.Cash.OpenAsync(q), _ => { completed++; return Task.CompletedTask; });
        c.Repository.FoundReceipt = new(1, id);
        await operation.CheckCommand.ExecuteAsync(null);
        Assert.False(operation.HasPending); Assert.Single(c.Repository.Writes); Assert.Equal(1, completed);
    }
    [Fact]
    public async Task FailedRefreshCannotReopenCommittedWrite()
    {
        var c = new Context(); var operation = c.Operation(); var id = Guid.NewGuid();
        await operation.StartAsync(new OpenCashShift(id, 1, 10, "Apertura"), id, q => c.Cash.OpenAsync(q), _ => throw new IOException("Read failed"));
        Assert.False(operation.HasPending); Assert.Contains("confirmada", operation.StatusMessage); Assert.Contains("Actualizar", operation.StatusMessage);
        await operation.RetryCommand.ExecuteAsync(null); Assert.Single(c.Repository.Writes);
    }
    [Fact]
    public async Task SessionChangeDropsOldResultAndDisablesOldForm()
    {
        var c = new Context(); var operation = c.Operation(); var result = new TaskCompletionSource<Result<MvpReceipt>>(); var id = Guid.NewGuid(); var completed = false;
        using var form = new MvpFormModel("Prueba", "Aceptar", operation, _ => Task.CompletedTask);
        var pending = operation.StartAsync(new OpenCashShift(id, 1, 1, "Apertura"), id, _ => result.Task, _ => { completed = true; return Task.CompletedTask; });
        c.Session.Clear(); c.Login(); result.SetResult(Result<MvpReceipt>.Success(new(1, id))); await pending;
        Assert.False(completed); Assert.False(operation.HasPending); Assert.False(form.CanEdit);
    }
    [Fact]
    public async Task ConflictRequiresReviewAndBlocksResubmissionFromSameDialog()
    {
        var c = new Context(); c.Repository.RejectCode = "CONCURRENCY_CONFLICT"; var operation = c.Operation();
        using var form = new MvpFormModel("Movimiento", "Registrar", operation, async f =>
        {
            var id = Guid.NewGuid(); await operation.StartAsync(new CashMovement(id, 1, new byte[8], "INGRESO", 5, "Prueba"), id, q => c.Cash.MoveAsync(q), _ => Task.CompletedTask);
        }) { Confirmed = true };
        await Save(form); Assert.False(form.CanEdit); Assert.False(operation.HasPending); await Save(form); Assert.Single(c.Repository.Writes);
    }
    [Fact]
    public async Task CashMoveUsesReadShiftVersionAndManualRecoveryKeepsAmount()
    {
        var c = new Context(); var vm = c.CashVm(); await vm.ReloadCommand.ExecuteAsync(null);
        var version = vm.Shift!.RowVersion.ToArray(); c.Repository.LoseResponse = true;
        c.Dialogs.Action = async form => { Set(form, "amount", "7"); Set(form, "reason", "Ingreso de prueba"); await Save(form); Set(form, "amount", "999"); };
        await vm.MoveCommand.ExecuteAsync(null);
        Assert.True(vm.Operation.HasPending); var request = Assert.IsType<CashMovement>(vm.Operation.Request); Assert.Equal(version, request.ExpectedVersion); Assert.Equal(7, request.Amount);
        c.Repository.LoseResponse = false; await vm.Operation.RetryCommand.ExecuteAsync(null);
        Assert.Equal(7, Assert.IsType<CashMovement>(c.Repository.Writes.Last()).Amount);
    }
    [Fact]
    public async Task CashCloseShowsServerArqueoAndAllowsNextOpening()
    {
        var c = new Context(); var vm = c.CashVm(); await vm.ReloadCommand.ExecuteAsync(null);
        c.Dialogs.Action = async f => { Assert.Contains("75", f.Description); Set(f, "amount", "70"); Set(f, "reason", "Diferencia revisada"); c.Repository.Shift!.State = "CERRADO"; c.Repository.Shift.Counted = 70; c.Repository.Shift.Difference = -5; await Save(f); };
        await vm.CloseCommand.ExecuteAsync(null);
        var request = Assert.IsType<CloseCashShift>(Assert.Single(c.Repository.Writes)); Assert.Equal(70, request.Counted); Assert.Equal(-5, vm.Shift!.Difference); Assert.True(vm.CanOpen);
    }
    [Fact]
    public async Task PosUsesServiceCartTokenAndTotalsAndSeparatesTenderedCash()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null);
        vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.CalculateCommand.ExecuteAsync(null); var quote = vm.Quote!;
        Assert.Equal(123.45m, quote.Total);
        c.Dialogs.Action = async f =>
        {
            var checkout = Assert.IsType<CheckoutModel>(f.Body); checkout.Payments.Add(new() { MethodId = 1, Method = "Efectivo", Amount = quote.Total }); checkout.CashTendered = "150"; Set(f, "reason", "Venta revisada"); await Save(f);
        };
        await vm.CheckoutCommand.ExecuteAsync(null);
        var request = Assert.IsType<ConfirmSale>(Assert.Single(c.Repository.Writes)); Assert.Equal(JsonSerializer.Serialize(quote.Cart), JsonSerializer.Serialize(request.Cart)); Assert.Equal(quote.Token, request.QuoteToken); Assert.Equal(150, request.CashTendered); Assert.Equal(quote.Total, request.Payments.Single().Amount); Assert.Empty(vm.Items); Assert.NotNull(vm.ConfirmedSale);
    }
    [Fact]
    public async Task PosDraftChangesInvalidateQuoteAndAuthorization()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.CalculateCommand.ExecuteAsync(null);
        vm.AuthorizationId = 10; vm.GlobalDiscount = "5"; Assert.Null(vm.Quote); Assert.Null(vm.AuthorizationId); Assert.False(vm.CanSell);
    }
    [Fact]
    public async Task PosDoesNotApplyQuoteCompletedAfterLogout()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); var tcs = new TaskCompletionSource<SaleQuote>(); c.Repository.QuoteTask = tcs.Task;
        var task = vm.CalculateCommand.ExecuteAsync(null); c.Session.Clear(); tcs.SetResult(c.Repository.QuoteFactory(new(1, null, [new(1, 1)]))); await task;
        Assert.Null(vm.Quote); Assert.Empty(vm.Items);
    }
    [Fact]
    public async Task QuotationConversionRepricesAndKeepsSourceIdButNotOldAuthorization()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null);
        var prior = c.Repository.QuoteFactory(new(1, 1, [new(1, 2)])) with { Total = 1 };
        c.Repository.Quotations.Add(new() { Id = 10, SnapshotJson = JsonSerializer.Serialize(prior) });
        await vm.LoadQuotationAsync(10);
        Assert.Equal(10, vm.QuotationId); Assert.Equal(123.45m, vm.Quote!.Total); Assert.Equal(2, vm.Items.Single().Quantity); Assert.Null(vm.AuthorizationId);
    }
    [Fact]
    public async Task ConversionDoesNotOverwriteAnExistingCart()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.LoadQuotationAsync(10);
        Assert.Single(vm.Items); Assert.Null(vm.QuotationId); Assert.Contains("carrito", vm.StatusMessage);
    }
    [Fact]
    public async Task CreditPaymentKeepsAccountVersionAndRefreshesCustomerHistory()
    {
        var c = new Context(); var vm = new CreditViewModel(c.Cash, c.Sales, c.Customers, c.Dialogs, c.Session, c.Operations, NullLogger<CreditViewModel>.Instance);
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.Single(); var version = vm.Selected.RowVersion.ToArray();
        c.Dialogs.Action = async f => { Set(f, "amount", "5"); Set(f, "reason", "Abono revisado"); await Save(f); };
        await vm.PayCommand.ExecuteAsync(null);
        var request = Assert.IsType<AccountPayment>(Assert.Single(c.Repository.Writes)); Assert.Equal(version, request.ExpectedVersion); Assert.Equal(1, request.ShiftId); Assert.Single(vm.Payments); Assert.NotNull(vm.Balance);
    }
    [Fact]
    public async Task OrderTransitionsOfferOnlyAllowedStatesAndRetainVersion()
    {
        var c = new Context(); var vm = c.OrdersVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items.Single(); await vm.DetailCommand.ExecuteAsync(null);
        c.Dialogs.Action = async f => { var states = f.Fields.Single(x => x.Key == "state").Choices; Assert.DoesNotContain("ENTREGADO", states); Set(f, "reason", "Preparando pedido"); await Save(f); };
        await vm.TransitionCommand.ExecuteAsync(null);
        var request = Assert.IsType<OrderTransition>(Assert.Single(c.Repository.Writes)); Assert.Equal("PREPARANDO", request.State); Assert.Equal(c.Repository.Order.Order.RowVersion, request.ExpectedVersion);
    }
    [Fact]
    public async Task ReadOnlySessionCannotOpenWriteDialogs()
    {
        var c = new Context(); c.Session.Begin(new(1, "prueba", "Prueba", 1, [], ["CAJA.VER", "VENTAS.VER", "CLIENTES.VER"], false));
        var cash = c.CashVm(); await cash.ReloadCommand.ExecuteAsync(null); await cash.MoveCommand.ExecuteAsync(null); Assert.Null(c.Dialogs.Last);
        var orders = c.OrdersVm(); await orders.CreateCommand.ExecuteAsync(null); Assert.Null(c.Dialogs.Last); Assert.Empty(c.Repository.Writes);
    }
    [Fact]
    public void CheckoutRequiresReferencesWithoutChangingCommercialTotals()
    {
        var c = new Context(); var quote = c.Repository.QuoteFactory(new(1, null, [new(1, 1)])); var checkout = new CheckoutModel(quote, c.Repository.Methods);
        checkout.Method = c.Repository.Methods[1]; checkout.Amount = "10"; checkout.AddCommand.Execute(null); Assert.Empty(checkout.Payments);
        checkout.Reference = "REFERENCIA-PRUEBA"; checkout.AddCommand.Execute(null); Assert.Single(checkout.Payments); Assert.Equal(123.45m, checkout.Quote.Total);
    }
    [Fact]
    public async Task InFlightWriteCannotBeReplacedByAnotherOperation()
    {
        var c = new Context(); var operation = c.Operation(); var result = new TaskCompletionSource<Result<MvpReceipt>>(); var id = Guid.NewGuid();
        var request = new OpenCashShift(id, 1, 1, "Apertura");
        var pending = operation.StartAsync(request, id, _ => result.Task, _ => Task.CompletedTask);
        var sends = 0;
        await operation.StartAsync(new OpenCashShift(Guid.NewGuid(), 1, 20, "Otra"), Guid.NewGuid(), _ => { sends++; return Task.FromResult(Result<MvpReceipt>.Success(new(2, Guid.NewGuid()))); }, _ => Task.CompletedTask);
        Assert.Same(request, operation.Request); Assert.Equal(0, sends);
        result.SetResult(Result<MvpReceipt>.Success(new(1, id))); await pending;
    }
    [Fact]
    public async Task OrderCreationKeepsSelectedAddressAndLineSnapshot()
    {
        var c = new Context(); var vm = c.OrdersVm();
        c.Dialogs.Action = async form =>
        {
            var draft = Assert.IsType<OrderDraftModel>(form.Body); draft.Customer.Selected = c.CustomersRepository.Customer;
            draft.Address = c.CustomersRepository.Addresses.Single(); draft.Items.Add(new(1, "SKU", "Producto", 3, 0));
            Set(form, "contact", "Contacto de prueba"); Set(form, "phone", "0000"); Set(form, "reason", "Pedido revisado"); await Save(form);
        };
        await vm.CreateCommand.ExecuteAsync(null);
        var request = Assert.IsType<PlaceOrder>(Assert.Single(c.Repository.Writes)); Assert.Equal(1, request.CustomerAddressId); Assert.Equal(1, request.CustomerId); Assert.Equal(3, request.Items.Single().Quantity);
    }
    [Fact]
    public async Task DiscountAuthorizationCanBeResolvedWithoutRetainingPassword()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 40)); await vm.CalculateCommand.ExecuteAsync(null);
        c.Repository.LoseResponse = true; var secret = "prueba-transitoria";
        c.Dialogs.Action = async form => { Set(form, "user", "administrador_prueba"); Set(form, "reason", "Descuento revisado"); form.ReadSecret = () => secret; form.ClearSecret = () => secret = ""; await Save(form); };
        await vm.AuthorizeCommand.ExecuteAsync(null);
        Assert.True(vm.Operation.HasPending); Assert.Equal("", secret); var request = Assert.IsType<DiscountApproval>(vm.Operation.Request);
        Assert.DoesNotContain("prueba-transitoria", JsonSerializer.Serialize(request));
        c.Repository.FoundReceipt = new(20, request.OperationId); await vm.Operation.CheckCommand.ExecuteAsync(null);
        Assert.Equal(20, vm.AuthorizationId); Assert.Single(c.Repository.Writes);
    }
    [Fact]
    public async Task QuotationEmitsCanonicalCartWithValidityAndAuthorization()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.CalculateCommand.ExecuteAsync(null); var token = vm.Quote!.Token;
        c.Dialogs.Action = async form => { form.Fields.Single(f => f.Key == "until").Date = DateTime.Today.AddDays(1); Set(form, "reason", "Cotización revisada"); await Save(form); };
        await vm.SaveQuotationCommand.ExecuteAsync(null);
        var request = Assert.IsType<SaveQuotation>(Assert.Single(c.Repository.Writes)); Assert.Equal(token, request.QuoteToken); Assert.Equal(DateTime.Today.AddDays(1), request.ValidUntil); Assert.Null(vm.Quote); Assert.Equal(10, vm.QuotationId); Assert.False(vm.CanChangeCart);
    }
    [Fact]
    public async Task PdfExportsConfirmedSnapshotToUserChosenDestination()
    {
        var c = new Context(); var files = new TestPdfFiles(); var renderer = DispatchProxy.Create<IReceiptRenderer, PdfPort>();
        var history = new CommercialHistoryViewModel(c.Sales, new SaleReceiptService(c.Repository, renderer, c.Session), files, c.PosVm(), new NavigationService(), c.Session, c.Operations, NullLogger<CommercialHistoryViewModel>.Instance);
        await history.ReloadSalesCommand.ExecuteAsync(null); history.SelectedSale = history.Sales.Single(); await history.ReadSaleCommand.ExecuteAsync(null); await history.ExportCommand.ExecuteAsync(null);
        Assert.Equal("GoDental-comprobante-interno-1.pdf", files.Name); Assert.Equal("%PDF-prueba", System.Text.Encoding.ASCII.GetString(files.Bytes!));
    }
    private sealed class TestPdfFiles : IPdfFileService
    {
        public byte[]? Bytes { get; private set; } public string? Name { get; private set; }
        public Task<bool> SaveAsync(byte[] pdf, string suggestedName) { Bytes = pdf; Name = suggestedName; return Task.FromResult(true); }
    }
    public class PdfPort : DispatchProxy { protected override object? Invoke(MethodInfo? method, object?[]? args) => System.Text.Encoding.ASCII.GetBytes("%PDF-prueba"); }
    public class IdentityPort : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(Result<UserIdentity>.Success(new(2, "administrador_prueba", "Administrador de prueba", 1, ["Administrador"], ["VENTAS.DESCUENTO_MAYOR_35"], false)));
    }
    [Fact]
    public async Task CancellingCustomerSelectionLeavesPosCustomerUnchanged()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null);
        c.Dialogs.Action = form => { Assert.IsType<CustomerLookupModel>(form.Body).Selected = c.CustomersRepository.Customer; return Task.CompletedTask; };
        await vm.SelectCustomerCommand.ExecuteAsync(null); Assert.Null(vm.Customer.Selected);
    }
    [Fact]
    public async Task RejectionOfRetryDoesNotEraseAnEarlierUncertainWrite()
    {
        var c = new Context(); var operation = c.Operation(); var id = Guid.NewGuid(); c.Repository.LoseResponse = true;
        var request = new OpenCashShift(id, 1, 10, "Apertura");
        await operation.StartAsync(request, id, q => c.Cash.OpenAsync(q), _ => Task.CompletedTask);
        c.Repository.LoseResponse = false; c.Repository.RejectCode = "FORBIDDEN";
        await operation.RetryCommand.ExecuteAsync(null);
        Assert.True(operation.HasPending); Assert.Same(request, operation.Request); Assert.False(operation.CanStart);
        c.Repository.FoundReceipt = new(1, id); await operation.CheckCommand.ExecuteAsync(null); Assert.False(operation.HasPending);
    }
    private static Task Save(MvpFormModel form) { form.Confirmed = true; return ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null); }
    private static void Set(MvpFormModel f, string key, string value) => f.Fields.Single(x => x.Key == key).Value = value;
    private sealed class Context
    {
        public UserSession Session { get; } = new(); public MvpUiRepository Repository { get; } = new(); public TestDialogs Dialogs { get; } = new();
        public CustomerWorkflowRepository CustomersRepository { get; } = new();
        public MvpOperationService Operations { get; } public CashService Cash { get; } public SalesService Sales { get; } public CustomerService Customers { get; }
        public Context() { Login(); Operations = new(Repository, Session); Cash = new(Repository, Session); Sales = new(Repository, Session, DispatchProxy.Create<IIdentityGateway, IdentityPort>()); Customers = new(CustomersRepository, Session); }
        public void Login() => Session.Begin(new(1, "prueba", "Prueba", 1, ["Administrador"], ["CAJA.VER", "CAJA.ABRIR", "CAJA.CERRAR", "CAJA.MOVIMIENTO", "AJUSTES.EDITAR", "VENTAS.VER", "VENTAS.CREAR", "VENTAS.COTIZAR", "VENTAS.REIMPRIMIR", "VENTAS.USAR_CREDITO", "CLIENTES.VER", "INVENTARIO.VER"], false));
        public MvpOperationModel Operation() => new(Session, Operations, NullLogger.Instance);
        public CashViewModel CashVm() => new(Cash, new InventoryService(DispatchProxy.Create<IInventoryRepository, UnusedPort>(), Session), Dialogs, Session, Operations, NullLogger<CashViewModel>.Instance);
        public PosViewModel PosVm() => new(Sales, Cash, Customers, Dialogs, Session, Operations, NullLogger<PosViewModel>.Instance);
        public OrdersViewModel OrdersVm() => new(new OrderService(Repository, Session), Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), Session), Dialogs, Session, Operations, NullLogger<OrdersViewModel>.Instance);
    }
    private sealed class TestDialogs : IMvpDialogService
    {
        public Func<MvpFormModel, Task>? Action { get; set; }
        public MvpFormModel? Last { get; private set; }
        public async Task<bool> ShowAsync(MvpFormModel form) { Last = form; if (Action is not null) await Action(form); return true; }
    }
    public class UnusedPort : DispatchProxy { protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Unexpected persistence call"); }
}
