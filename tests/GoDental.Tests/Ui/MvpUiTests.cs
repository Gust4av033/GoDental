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
        Assert.Same(quote.Lines.Single(), vm.CartRows.Single().Calculation);
        c.Dialogs.Action = async f =>
        {
            var checkout = Assert.IsType<CheckoutModel>(f.Body); checkout.Payments.Add(new() { MethodId = 1, Method = "Efectivo", Amount = quote.Total }); checkout.CashTendered = "150"; Set(f, "reason", "Venta revisada"); await Save(f);
        };
        await vm.CheckoutCommand.ExecuteAsync(null);
        var request = Assert.IsType<ConfirmSale>(Assert.Single(c.Repository.Writes)); Assert.Equal(JsonSerializer.Serialize(quote.Cart), JsonSerializer.Serialize(request.Cart)); Assert.Equal(quote.Token, request.QuoteToken); Assert.Equal(150, request.CashTendered); Assert.Equal(quote.Total, request.Payments.Single().Amount); Assert.Empty(vm.Items); Assert.NotNull(vm.ConfirmedSale);
    }
    [Fact]
    public async Task NewSaleClearsPriorCustomerAndChargesWithoutAnotherWrite()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null);
        vm.Customer.Selected = c.CustomersRepository.Customer; vm.GlobalDiscount = "5"; vm.DeliveryCharge = "3";
        vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.CalculateCommand.ExecuteAsync(null);
        c.Dialogs.Action = async form =>
        {
            var checkout = Assert.IsType<CheckoutModel>(form.Body);
            checkout.Payments.Add(new() { MethodId = 1, Amount = checkout.Quote.Total }); checkout.CashTendered = "150";
            Set(form, "reason", "Venta revisada"); await Save(form);
        };
        await vm.CheckoutCommand.ExecuteAsync(null);
        Assert.True(vm.HasConfirmedSale);
        vm.NewSaleCommand.Execute(null);
        Assert.False(vm.HasConfirmedSale); Assert.Null(vm.Customer.Selected);
        Assert.Equal("0", vm.GlobalDiscount); Assert.Equal("0", vm.DeliveryCharge);
        Assert.Empty(vm.CartRows); Assert.Null(vm.Quote); Assert.Single(c.Repository.Writes);
    }

    [Fact]
    public async Task PosDraftChangesInvalidateQuoteAndAuthorization()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.CalculateCommand.ExecuteAsync(null);
        vm.AuthorizationId = 10; vm.GlobalDiscount = "5"; Assert.Null(vm.Quote); Assert.Null(vm.AuthorizationId); Assert.False(vm.CanSell); Assert.Null(vm.CartRows.Single().Calculation);
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
        vm.ConfirmedSale = c.Repository.Sale; Assert.False(vm.CanChooseCustomer);
        await vm.LoadQuotationAsync(10);
        Assert.False(vm.HasConfirmedSale);
        Assert.Equal(10, vm.QuotationId); Assert.Equal(123.45m, vm.Quote!.Total); Assert.Equal(2, vm.Items.Single().Quantity); Assert.Null(vm.AuthorizationId);
    }
    [Fact]
    public async Task ConversionDoesNotOverwriteAnExistingCart()
    {
        var c = new Context(); var vm = c.PosVm(); await vm.ReloadCommand.ExecuteAsync(null); vm.Items.Add(new(1, "SKU", "Prueba", 1, 0)); await vm.LoadQuotationAsync(10);
        Assert.Single(vm.Items); Assert.Null(vm.QuotationId); Assert.Contains("carrito", vm.StatusMessage);
    }
    [Fact]
    public async Task OrderToPosRepricesKeepsOriginAndRetriesTheSameCombinedConfirmation()
    {
        var c = new Context(); var vm = c.PosVm();
        c.Repository.Order.Order.DeliveryCharge = 5;
        c.Repository.Order.Lines.Single().Quantity = 2;
        Assert.True(await vm.LoadOrderAsync(1));
        Assert.Equal(c.Repository.Order.Order.CustomerId, vm.Customer.Selected!.Id);
        Assert.Equal(2, vm.Items.Single().Quantity); Assert.Equal(5, vm.Quote!.Cart.DeliveryCharge);
        Assert.Equal(123.45m, vm.Quote.Total); Assert.False(vm.CanChangeCart); Assert.True(vm.CanAdjustPricing); Assert.False(vm.CanQuote);
        var version = vm.OrderOrigin!.ExpectedVersion.ToArray();
        c.Repository.LoseResponse = true;
        c.Dialogs.Action = async f =>
        {
            var checkout = Assert.IsType<CheckoutModel>(f.Body);
            checkout.Payments.Add(new() { MethodId = 1, Amount = checkout.Quote.Total }); checkout.CashTendered = "150";
            Set(f, "reason", "Cobrar pedido"); await Save(f);
        };
        await vm.CheckoutCommand.ExecuteAsync(null);
        var request = Assert.IsType<ConfirmSale>(Assert.Single(c.Repository.Writes));
        Assert.Equal(1, request.Order!.OrderId); Assert.Equal(version, request.Order.ExpectedVersion); Assert.Null(request.QuotationId);
        Assert.True(vm.Operation.HasPending); Assert.NotNull(vm.OrderOrigin); Assert.False(await vm.LoadOrderAsync(1));
        c.Repository.LoseResponse = false; await vm.Operation.RetryCommand.ExecuteAsync(null);
        Assert.Equal(request.OperationId, Assert.IsType<ConfirmSale>(c.Repository.Writes[1]).OperationId);
        Assert.Equal(2, c.Repository.Writes.Count); Assert.False(vm.Operation.HasPending); Assert.True(vm.HasConfirmedSale);
        Assert.Contains("vinculada", vm.StatusMessage); Assert.Equal(1, vm.OrderOrigin!.OrderId);
        vm.NewSaleCommand.Execute(null); Assert.Null(vm.OrderOrigin); Assert.Null(vm.Customer.Selected); Assert.Equal("0", vm.DeliveryCharge);
    }
    [Theory]
    [InlineData("CANCELADO", null)]
    [InlineData("ENTREGADO", null)]
    [InlineData("PREPARANDO", 1L)]
    public async Task OrderToPosRejectsFinalizedOrAlreadyBilledOrders(string state, long? sale)
    {
        var c = new Context(); c.Repository.Order.Order.State = state; c.Repository.Order.Order.SaleId = sale;
        var vm = c.PosVm(); Assert.False(await vm.LoadOrderAsync(1));
        Assert.Empty(vm.Items); Assert.Null(vm.OrderOrigin); Assert.Null(vm.Quote); Assert.Empty(c.Repository.Writes);
    }
    [Fact]
    public async Task OrderToPosPreservesExistingCartAndDoesNotApplyFailedOrLateLoads()
    {
        var c = new Context(); var vm = c.PosVm(); vm.Items.Add(new(1, "SKU", "En curso", 3, 0));
        Assert.False(await vm.LoadOrderAsync(1)); Assert.Equal(3, vm.Items.Single().Quantity); Assert.Null(vm.OrderOrigin);
        vm.Items.Clear(); c.Repository.QuoteFactory = _ => throw new ArgumentException("Sin stock disponible");
        Assert.False(await vm.LoadOrderAsync(1)); Assert.Empty(vm.Items); Assert.Null(vm.OrderOrigin); Assert.Contains("stock", vm.StatusMessage);
        var pending = new TaskCompletionSource<OrderSnapshot?>(); c.Repository.OrderReadTask = pending.Task;
        var load = vm.LoadOrderAsync(1); c.Session.Clear(); pending.SetResult(c.Repository.Order);
        Assert.False(await load); Assert.Empty(vm.Items); Assert.Null(vm.OrderOrigin);
    }
    [Fact]
    public async Task OrderDiscountRecalculationRefreshesVersionAndClearOnlyDiscardsLocalDraft()
    {
        var c = new Context(); var vm = c.PosVm(); Assert.True(await vm.LoadOrderAsync(1));
        vm.GlobalDiscount = "5"; Assert.Null(vm.Quote); Assert.Equal(1, vm.OrderOrigin!.OrderId);
        c.Repository.Order.Order.RowVersion = [3,3,3,3,3,3,3,3];
        await vm.CalculateCommand.ExecuteAsync(null);
        Assert.Equal(5, vm.Quote!.Cart.GlobalDiscountPercent); Assert.Equal(c.Repository.Order.Order.RowVersion, vm.OrderOrigin!.ExpectedVersion);
        c.Dialogs.Action = async f => await Save(f); await vm.ClearCartCommand.ExecuteAsync(null);
        Assert.Null(vm.OrderOrigin); Assert.Empty(vm.Items); Assert.Null(vm.Customer.Selected); Assert.Empty(c.Repository.Writes);
    }
    [Fact]
    public async Task OrderWithSplitLotsKeepsOneCartItemAndShowsAllReturnedAmounts()
    {
        var c = new Context(); var vm = c.PosVm(); c.Repository.Order.Lines.Single().Quantity = 2;
        var factory = c.Repository.QuoteFactory;
        c.Repository.QuoteFactory = cart =>
        {
            var result = factory(cart); var first = result.Lines.Single() with { Quantity = 1, Total = 12.34m, Lots = [new(1, 1)] };
            return result with { Lines = [first, first with { Total = 23.45m, Lots = [new(2, 1)] }], Total = 35.79m };
        };
        Assert.True(await vm.LoadOrderAsync(1)); Assert.Single(vm.Items); Assert.Equal(2, vm.Items.Single().Quantity);
        var row = Assert.Single(vm.CartRows); Assert.Equal(2, row.Quantity); Assert.Equal(35.79m, row.Calculation!.Total); Assert.Equal(2, row.Calculation.Lots.Count);
    }
    [Fact]
    public async Task OrdersActionNavigatesToLoadedPosAndRespectsReadOnlyPermissions()
    {
        var c = new Context(); var pos = c.PosVm(); var navigation = new NavigationService();
        var orders = new OrdersViewModel(new(c.Repository, c.Session), c.Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), c.Session), pos, navigation, c.Dialogs, c.Session, c.Operations, NullLogger<OrdersViewModel>.Instance);
        await orders.OpenOrderCommand.ExecuteAsync(c.Repository.Order.Order);
        await orders.PassToPosCommand.ExecuteAsync(null);
        Assert.Equal(SetupPage.Pos, navigation.CurrentPage); Assert.Equal(1, pos.OrderOrigin!.OrderId);
        c.Session.Begin(new(1, "prueba", "Prueba", 1, [], ["VENTAS.VER", "CAJA.VER", "CLIENTES.VER"], false));
        await orders.OpenOrderCommand.ExecuteAsync(c.Repository.Order.Order); Assert.False(orders.CanPassToPos);
        Assert.False(await pos.LoadOrderAsync(1)); Assert.Empty(pos.Items); Assert.Empty(c.Repository.Writes);
    }
    [Fact]
    public async Task SaleOriginIsValidatedAndItsVersionCopiedAtApplicationBoundary()
    {
        var c = new Context(); var quote = c.Repository.QuoteFactory(new(1, 1, [new(1, 1)]));
        var request = new ConfirmSale(Guid.NewGuid(), quote.Cart, quote.Token, [new(1, quote.Total)], 150, 1, "Pedido", Order: new(1, [1,2,3,4,5,6,7,8]));
        Assert.Equal("VALIDATION", (await c.Sales.ConfirmAsync(request with { Order = new(0, request.Order!.ExpectedVersion) })).ErrorCode);
        Assert.Equal("VALIDATION", (await c.Sales.ConfirmAsync(request with { Order = new(1, []) })).ErrorCode);
        Assert.Equal("VALIDATION", (await c.Sales.ConfirmAsync(request with { QuotationId = 1 })).ErrorCode);
        Assert.True((await c.Sales.ConfirmAsync(request)).IsSuccess);
        var saved = Assert.IsType<ConfirmSale>(Assert.Single(c.Repository.Writes));
        request.Order!.ExpectedVersion[0] = 99; Assert.Equal(1, saved.Order!.ExpectedVersion[0]);
    }
    [Fact]
    public async Task CreditPaymentKeepsAccountVersionAndRefreshesCustomerHistory()
    {
        var c = new Context(); var vm = new CreditViewModel(c.Cash, c.Sales, c.Customers, c.Dialogs, c.Session, c.Operations, NullLogger<CreditViewModel>.Instance);
        await vm.ReloadCommand.ExecuteAsync(null); await vm.OpenAccountCommand.ExecuteAsync(vm.Items.Single()); Assert.True(vm.IsDetailOpen); var version = vm.Selected!.RowVersion.ToArray();
        c.Dialogs.Action = async f => { Set(f, "amount", "5"); Set(f, "reason", "Abono revisado"); await Save(f); };
        await vm.PayCommand.ExecuteAsync(null);
        var request = Assert.IsType<AccountPayment>(Assert.Single(c.Repository.Writes)); Assert.Equal(version, request.ExpectedVersion); Assert.Equal(1, request.ShiftId); Assert.Single(vm.Payments); Assert.NotNull(vm.Balance); Assert.True(vm.IsDetailOpen);
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
    public async Task OrderListAndDetailNavigationPreservesFilterAndClearsOnLogout()
    {
        var c = new Context(); var vm = c.OrdersVm();
        await vm.ReloadCommand.ExecuteAsync(null);
        await vm.OpenOrderCommand.ExecuteAsync(vm.Items.Single());
        Assert.True(vm.IsDetailOpen);
        vm.State = "RECIBIDO";
        vm.ReturnToListCommand.Execute(null);
        Assert.False(vm.IsDetailOpen); Assert.Equal("RECIBIDO", vm.State); Assert.NotNull(vm.Selected);
        await vm.OpenOrderCommand.ExecuteAsync(vm.Selected);
        Assert.True(vm.IsDetailOpen);
        c.Session.Clear(); Assert.False(vm.IsDetailOpen); Assert.Null(vm.Detail); Assert.Empty(vm.Events);
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
            draft.ContactName = "Contacto de prueba"; draft.Phone = "0000"; Set(form, "reason", "Pedido revisado"); await Save(form);
        };
        await vm.CreateCommand.ExecuteAsync(null);
        var request = Assert.IsType<PlaceOrder>(Assert.Single(c.Repository.Writes)); Assert.Equal(1, request.CustomerAddressId); Assert.Equal(1, request.CustomerId); Assert.Equal(3, request.Items.Single().Quantity); Assert.Equal("Contacto de prueba", request.Contact); Assert.Equal("Dirección de prueba", request.Address);
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
        var history = new CommercialHistoryViewModel(c.Sales, new SaleReceiptService(c.Repository, renderer, c.Session), files, files, c.PosVm(), new NavigationService(), c.Session, c.Operations, NullLogger<CommercialHistoryViewModel>.Instance);
        await history.ReloadSalesCommand.ExecuteAsync(null); history.SelectedSale = history.Sales.Single(); await history.ReadSaleCommand.ExecuteAsync(null);
        Assert.True(history.IsSaleDetailOpen); var selected = history.SelectedSale;
        history.ReturnToSalesCommand.Execute(null); Assert.False(history.IsSaleDetailOpen); Assert.Same(selected, history.SelectedSale);
        await history.OpenSaleCommand.ExecuteAsync(selected); Assert.True(history.IsSaleDetailOpen);
        await history.ExportCommand.ExecuteAsync(null);
        Assert.Equal("GoDental-comprobante-interno-1.pdf", files.Name); Assert.Equal("%PDF-prueba", System.Text.Encoding.ASCII.GetString(files.Bytes!));
    }
    private sealed class TestPdfFiles : IPdfFileService, IPdfPrintService
    {
        public byte[]? Bytes { get; private set; } public string? Name { get; private set; }
        public ReceiptDocument? Printed { get; private set; }
        public Task<bool> SaveAsync(byte[] pdf, string suggestedName) { Bytes = pdf; Name = suggestedName; return Task.FromResult(true); }
        public Task<bool> PrintAsync(ReceiptDocument document, string name) { Printed = document; return Task.FromResult(false); }
    }
    [Fact]
    public async Task ReceiptPreviewNavigatesAndSavesAndPrintsTheReviewedDocumentWithoutBusinessWrites()
    {
        var c = new Context(); var files = new TestPdfFiles();
        var history = new CommercialHistoryViewModel(c.Sales, new SaleReceiptService(c.Repository, DispatchProxy.Create<IReceiptRenderer, PdfPort>(), c.Session), files, files, c.PosVm(), new NavigationService(), c.Session, c.Operations, NullLogger<CommercialHistoryViewModel>.Instance);
        await history.PreviewSaleCommand.ExecuteAsync(1L);
        Assert.True(history.IsReceiptPreviewOpen); var document = history.ReceiptPreview!;
        Assert.Same(document.Pages[0], history.PreviewPage);
        history.NextPreviewPageCommand.Execute(null); history.NextPreviewPageCommand.Execute(null);
        Assert.Equal(2, history.PreviewPageNumber); Assert.Same(document.Pages[1], history.PreviewPage);
        history.PreviousPreviewPageCommand.Execute(null); history.PreviousPreviewPageCommand.Execute(null); Assert.Equal(1, history.PreviewPageNumber);
        for (var i = 0; i < 10; i++) history.ZoomInCommand.Execute(null); Assert.Equal(2, history.PreviewZoom);
        for (var i = 0; i < 10; i++) history.ZoomOutCommand.Execute(null); Assert.Equal(.5m, history.PreviewZoom);
        await history.ExportCommand.ExecuteAsync(null); Assert.Same(document.Pdf, files.Bytes);
        await history.PrintReceiptCommand.ExecuteAsync(null); Assert.Same(document, files.Printed); Assert.Contains("canceló", history.StatusMessage);
        Assert.Empty(c.Repository.Writes); Assert.True(history.IsReceiptPreviewOpen);
        history.CloseReceiptPreviewCommand.Execute(null); Assert.False(history.IsReceiptPreviewOpen); Assert.True(history.IsSaleDetailOpen);
        await history.PreviewReceiptCommand.ExecuteAsync(null); c.Session.Clear(); Assert.Null(history.ReceiptPreview); Assert.Null(history.PreviewPage);
    }
    [Fact]
    public async Task ReceiptPreviewRejectsMissingPermissionAndDiscardsOutdatedSelectionOrSession()
    {
        var c = new Context(); var files = new TestPdfFiles();
        var history = new CommercialHistoryViewModel(c.Sales, new SaleReceiptService(c.Repository, DispatchProxy.Create<IReceiptRenderer, PdfPort>(), c.Session), files, files, c.PosVm(), new NavigationService(), c.Session, c.Operations, NullLogger<CommercialHistoryViewModel>.Instance);
        await history.OpenSaleCommand.ExecuteAsync(c.Repository.Sale.Sale);
        var pending = new TaskCompletionSource<SaleSnapshot?>(); c.Repository.ReceiptReadTask = pending.Task;
        var load = history.PreviewReceiptCommand.ExecuteAsync(null); history.SelectedSale = new() { Id = 2 }; pending.SetResult(c.Repository.Sale); await load;
        Assert.Null(history.ReceiptPreview);
        await history.OpenSaleCommand.ExecuteAsync(c.Repository.Sale.Sale);
        pending = new(); c.Repository.ReceiptReadTask = pending.Task;
        load = history.PreviewReceiptCommand.ExecuteAsync(null); c.Session.Clear(); pending.SetResult(c.Repository.Sale); await load;
        Assert.Null(history.ReceiptPreview); Assert.Null(files.Bytes); Assert.Null(files.Printed);
        c.Session.Begin(new(1, "lector", "Lector", 1, [], ["VENTAS.VER"], false));
        await history.PreviewSaleCommand.ExecuteAsync(1L); Assert.Null(history.ReceiptPreview); Assert.Empty(c.Repository.Writes);
    }
    [Fact] public async Task ChangedReceiptFormatMustBeReviewedBeforeSavingOrPrinting()
    {
        var c = new Context(); var files = new TestPdfFiles();
        var history = new CommercialHistoryViewModel(c.Sales, new SaleReceiptService(c.Repository, new GoDental.Infrastructure.Operations.ReceiptPdfRenderer(Microsoft.Extensions.Options.Options.Create(new GoDental.Infrastructure.Operations.ReceiptPdfOptions())), c.Session), files, files, c.PosVm(), new NavigationService(), c.Session, c.Operations, NullLogger<CommercialHistoryViewModel>.Instance);
        await history.ReloadSalesCommand.ExecuteAsync(null); history.SelectedSale = history.Sales.Single(); await history.ReadSaleCommand.ExecuteAsync(null);
        await history.PreviewReceiptCommand.ExecuteAsync(null); Assert.True(history.CanUsePreview);
        history.PrintSettings.Paper = "Ticket 58 mm"; Assert.False(history.CanUsePreview);
        await history.ExportCommand.ExecuteAsync(null); await history.PrintReceiptCommand.ExecuteAsync(null); Assert.Null(files.Bytes); Assert.Null(files.Printed);
        await history.PreviewReceiptCommand.ExecuteAsync(null); Assert.True(history.CanUsePreview); Assert.Equal(58, history.ReceiptPreview!.WidthMm);
        Assert.InRange(history.PreviewWidth, 164, 165); Assert.Empty(c.Repository.Writes);
    }
    public class PdfPort : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes("%PDF-prueba");
            return method?.Name == nameof(IReceiptRenderer.RenderPreview) ? new ReceiptDocument(bytes, [new byte[] { 1 }, new byte[] { 2 }]) : bytes;
        }
    }
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
    [Fact]
    public async Task OrderCustomerSelectionPrefillsDeliveryAndManualEditDetachesSavedAddress()
    {
        var c = new Context(); var draft = new OrderDraftModel(c.Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), c.Session), () => c.Session.Revision);
        draft.Customer.Selected = c.CustomersRepository.Customer;
        await (draft.AddressesCommand.ExecutionTask ?? Task.CompletedTask);
        Assert.Equal("Contacto de prueba", draft.ContactName); Assert.Equal("00000000", draft.Phone);
        Assert.Equal("Dirección de prueba", draft.DeliveryAddress); Assert.Equal(1, draft.Address!.Id);
        draft.DeliveryAddress = "Dirección sólo para este pedido";
        Assert.Null(draft.Address); Assert.Equal("Dirección de prueba", c.CustomersRepository.Addresses.Single().Address);
        draft.Customer.Selected = null;
        Assert.Empty(draft.DeliveryAddress); Assert.Empty(draft.ContactName); Assert.Empty(draft.Phone);
    }
    [Fact]
    public async Task OrderCustomerPrefillIgnoresEarlierReadAndClearsPreviousDataImmediately()
    {
        var c = new Context(); var draft = new OrderDraftModel(c.Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), c.Session), () => c.Session.Revision);
        var first = new TaskCompletionSource<CustomerSnapshot?>(); c.CustomersRepository.PendingRead = first;
        draft.Customer.Selected = c.CustomersRepository.Customer; var firstLoad = draft.AddressesCommand.ExecutionTask!;
        var second = new TaskCompletionSource<CustomerSnapshot?>(); c.CustomersRepository.PendingRead = second;
        var other = c.CustomersRepository.Customer with { Id = 2, Name = "Otro cliente", Phone = "2222" };
        draft.Customer.Selected = other; var secondLoad = draft.AddressesCommand.ExecutionTask!;
        Assert.Empty(draft.DeliveryAddress); Assert.False(draft.CanEditDelivery);
        second.SetResult(new(other, new(), [], [new() { Id = 2, Address = "Dirección del segundo cliente" }])); await secondLoad;
        first.SetResult(new(c.CustomersRepository.Customer, new(), c.CustomersRepository.Contacts, c.CustomersRepository.Addresses)); await firstLoad;
        Assert.Equal("Otro cliente", draft.ContactName); Assert.Equal("2222", draft.Phone);
        Assert.Equal("Dirección del segundo cliente", draft.DeliveryAddress); Assert.Equal(2, draft.Address!.Id); Assert.True(draft.CanEditDelivery);
    }
    [Fact]
    public async Task OrderPrefillRequiresChoiceWhenSeveralAddressesArePrimary()
    {
        var c = new Context(); c.CustomersRepository.Addresses[0].Primary = true;
        c.CustomersRepository.Addresses.Add(new() { Id = 2, Primary = true, Address = "Otra dirección" });
        c.CustomersRepository.Addresses.Add(new() { Id = 3, Active = false, Address = "No disponible" });
        var draft = new OrderDraftModel(c.Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), c.Session), () => c.Session.Revision);
        draft.Customer.Selected = c.CustomersRepository.Customer; await (draft.AddressesCommand.ExecutionTask ?? Task.CompletedTask);
        Assert.Equal(2, draft.Addresses.Count); Assert.Null(draft.Address); Assert.Empty(draft.DeliveryAddress);
    }
    [Fact]
    public async Task InitialInventoryUsesExistingContractAndRefreshesAfterConfirmation()
    {
        var c = new Context(); var vm = new InitialInventoryViewModel(new(c.Repository, c.Session), c.Dialogs, c.Session, c.Operations, NullLogger<InitialInventoryViewModel>.Instance);
        var refreshes = 0;
        c.Dialogs.Action = async f => { Set(f, "quantity", "3"); Set(f, "cost", "1.123456"); Set(f, "lot", "INICIAL-1"); Set(f, "reason", "Inventario verificado"); f.Fields.Single(x => x.Key == "expires").Date = new DateTime(2030, 1, 1); await Save(f); };
        await vm.OpenAsync(new() { Id = 4, Name = "Producto", TracksLot = true, TracksExpiry = true }, new() { Id = 2, Name = "Almacén" }, () => { refreshes++; return Task.CompletedTask; });
        var request = Assert.IsType<InitialInventory>(Assert.Single(c.Repository.Writes));
        Assert.Equal(4, request.ProductId); Assert.Equal(2, request.WarehouseId); Assert.Equal(3, request.Quantity); Assert.Equal(1.123456m, request.UnitCost);
        Assert.Equal("INICIAL-1", request.LotNumber); Assert.Equal(new DateTime(2030, 1, 1), request.Expires); Assert.NotEqual(Guid.Empty, request.OperationId); Assert.Equal(1, refreshes);
    }
    [Fact]
    public async Task InitialInventoryRetainsSameRequestAfterLostResponse()
    {
        var c = new Context(); var vm = new InitialInventoryViewModel(new(c.Repository, c.Session), c.Dialogs, c.Session, c.Operations, NullLogger<InitialInventoryViewModel>.Instance);
        var refreshes = 0; c.Repository.LoseResponse = true;
        c.Dialogs.Action = async f => { Set(f, "quantity", "1"); Set(f, "cost", "2"); Set(f, "reason", "Carga revisada"); await Save(f); };
        await vm.OpenAsync(new() { Id = 1 }, new() { Id = 1 }, () => { refreshes++; return Task.CompletedTask; });
        Assert.True(vm.Operation.HasPending); Assert.False(vm.CanInitialize); var original = vm.Operation.Request;
        c.Repository.LoseResponse = false; await vm.Operation.RetryCommand.ExecuteAsync(null);
        Assert.Equal(Assert.IsType<InitialInventory>(original), Assert.IsType<InitialInventory>(c.Repository.Writes[1])); Assert.Equal(1, refreshes); Assert.False(vm.Operation.HasPending);
    }
    [Fact]
    public async Task InitialInventoryReaderCannotOpenWriteForm()
    {
        var c = new Context(); c.Session.Begin(new(1, "lector", "Lector", 1, [], ["INVENTARIO.VER"], false));
        var vm = new InitialInventoryViewModel(new(c.Repository, c.Session), c.Dialogs, c.Session, c.Operations, NullLogger<InitialInventoryViewModel>.Instance);
        await vm.OpenAsync(new() { Id = 1 }, new() { Id = 1 }, () => Task.CompletedTask);
        Assert.Null(c.Dialogs.Last); Assert.Empty(c.Repository.Writes);
    }
    private static Task Save(MvpFormModel form) { form.Confirmed = true; return ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null); }
    private static void Set(MvpFormModel f, string key, string value) => f.Fields.Single(x => x.Key == key).Value = value;
    private sealed class Context
    {
        public UserSession Session { get; } = new(); public MvpUiRepository Repository { get; } = new(); public TestDialogs Dialogs { get; } = new();
        public CustomerWorkflowRepository CustomersRepository { get; } = new();
        public MvpOperationService Operations { get; } public CashService Cash { get; } public SalesService Sales { get; } public CustomerService Customers { get; }
        public Context() { Login(); Operations = new(Repository, Session); Cash = new(Repository, Session); Sales = new(Repository, Session, DispatchProxy.Create<IIdentityGateway, IdentityPort>()); Customers = new(CustomersRepository, Session); }
        public void Login() => Session.Begin(new(1, "prueba", "Prueba", 1, ["Administrador"], ["CAJA.VER", "CAJA.ABRIR", "CAJA.CERRAR", "CAJA.MOVIMIENTO", "AJUSTES.EDITAR", "VENTAS.VER", "VENTAS.CREAR", "VENTAS.COTIZAR", "VENTAS.REIMPRIMIR", "VENTAS.USAR_CREDITO", "CLIENTES.VER", "INVENTARIO.VER", "INVENTARIO.AJUSTAR"], false));
        public MvpOperationModel Operation() => new(Session, Operations, NullLogger.Instance);
        public CashViewModel CashVm() => new(Cash, new InventoryService(DispatchProxy.Create<IInventoryRepository, UnusedPort>(), Session), Dialogs, Session, Operations, NullLogger<CashViewModel>.Instance);
        public PosViewModel PosVm() => new(Sales, Cash, Customers, new OrderService(Repository, Session), Dialogs, Session, Operations, NullLogger<PosViewModel>.Instance);
        public OrdersViewModel OrdersVm() => new(new OrderService(Repository, Session), Customers, new ProductService(DispatchProxy.Create<IProductRepository, UnusedPort>(), Session), PosVm(), new NavigationService(), Dialogs, Session, Operations, NullLogger<OrdersViewModel>.Instance);
    }
    private sealed class TestDialogs : IMvpDialogService
    {
        public Func<MvpFormModel, Task>? Action { get; set; }
        public MvpFormModel? Last { get; private set; }
        public async Task<bool> ShowAsync(MvpFormModel form) { Last = form; if (Action is not null) await Action(form); return true; }
    }
    public class UnusedPort : DispatchProxy { protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new InvalidOperationException("Unexpected persistence call"); }
}
