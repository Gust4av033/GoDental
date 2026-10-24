using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;

public sealed class WorkflowUiTests
{
    private static UserSession Session(params string[] permissions)
    {
        var s = new UserSession();
        s.Begin(new(1, "prueba", "Prueba", 1, [], permissions.Length == 0 ? ["INVENTARIO.VER", "INVENTARIO.CONTEO", "INVENTARIO.AJUSTAR", "COMPRAS.VER", "PROVEEDORES.GESTIONAR"] : permissions, false));
        return s;
    }
    private static InventoryCountsViewModel Counts(UiCountRepository repo, UserSession session, Dialogs? dialogs = null)
    {
        var catalog = new CatalogRepository();
        return new(new(repo, session), new(catalog, session), new(catalog, session), session, dialogs ?? new(), NullLogger<InventoryCountsViewModel>.Instance);
    }
    private static SuppliersViewModel Suppliers(UiSupplierRepository repo, UserSession session, Dialogs dialogs) =>
        new(new(repo, session), new(new CatalogRepository(), session), session, dialogs, NullLogger<SuppliersViewModel>.Instance);
    private static async Task Open(InventoryCountsViewModel vm)
    {
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items[0]; await vm.OpenCommand.ExecuteAsync(null);
    }
    private static async Task Open(SuppliersViewModel vm)
    {
        await vm.ReloadCommand.ExecuteAsync(null); vm.Selected = vm.Items[0]; await vm.OpenCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task ReturnToSupplierListPreservesSelectionAndSessionClearsDetail()
    {
        var session = Session();
        var vm = Suppliers(new(), session, new());
        await Open(vm);
        var selected = vm.Selected;
        vm.Search = "Proveedor";
        Assert.True(vm.IsDetailOpen);
        vm.ReturnToListCommand.Execute(null);
        Assert.False(vm.IsDetailOpen);
        Assert.Same(selected, vm.Selected);
        Assert.Equal("Proveedor", vm.Search);
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
    public async Task SupplierRowOpensWithoutPreviousSelectionAndRequiresReadPermission()
    {
        var session = Session("COMPRAS.VER");
        var vm = Suppliers(new(), session, new());
        await vm.ReloadCommand.ExecuteAsync(null);
        var row = vm.Items.Single();
        Assert.Null(vm.Selected);
        await vm.OpenSupplierCommand.ExecuteAsync(row);
        Assert.Same(row, vm.Selected);
        Assert.Equal(row.Id, vm.Detail!.Id);
        Assert.True(vm.IsDetailOpen);
        session.Clear();
        await vm.OpenSupplierCommand.ExecuteAsync(row);
        Assert.Null(vm.Detail);
        Assert.Null(vm.Selected);

        var deniedSession = Session("INVENTARIO.VER");
        var denied = Suppliers(new() { DenyRead = true }, deniedSession, new());
        await denied.OpenSupplierCommand.ExecuteAsync(row);
        Assert.Null(denied.Selected);
        Assert.Null(denied.Detail);
        Assert.True(deniedSession.IsAuthenticated);
    }

    [Fact]
    public async Task CountNavigationPreservesCaptureUntilSavedOrExplicitlyDiscarded()
    {
        var session = Session();
        var vm = Counts(new(), session);
        await Open(vm);
        Assert.True(vm.IsDetailOpen);
        var selected = vm.Selected;
        vm.Lines[0].Quantity = "0";
        vm.ReturnToListCommand.Execute(null);
        Assert.True(vm.IsDetailOpen);
        await vm.RefreshDetailCommand.ExecuteAsync(null);
        Assert.Equal("0", vm.Lines[0].Quantity);
        vm.DiscardCaptureCommand.Execute(null);
        Assert.Equal("", vm.Lines[0].Quantity);
        vm.ReturnToListCommand.Execute(null);
        Assert.False(vm.IsDetailOpen);
        Assert.Same(selected, vm.Selected);
        await vm.OpenCommand.ExecuteAsync(null);
        Assert.True(vm.IsDetailOpen);
        session.Clear();
        Assert.False(vm.IsDetailOpen);
        Assert.Null(vm.Header);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningUsesSelectedProductsAndLostResponseBlocksRetry(bool loseResponse)
    {
        var repo = new UiCountRepository { LoseStartResponse = loseResponse }; var dialogs = new Dialogs(); var vm = Counts(repo, Session(), dialogs);
        await vm.ReloadCommand.ExecuteAsync(null);
        dialogs.Count = async form =>
        {
            await form.Picker.SearchCommand.ExecuteAsync(null); form.Picker.Items[0].Selected = true;
            form.Reason = "Conteo parcial de prueba"; form.Confirmed = true;
            await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);
            Assert.Equal(!loseResponse, form.CanSubmit);
            return !loseResponse;
        };
        await vm.StartCommand.ExecuteAsync(null);
        Assert.Equal(1, repo.LastStart!.WarehouseId); Assert.Equal(new[] { 1 }, repo.LastStart.ProductIds);
    }

    [Fact]
    public async Task PendingIsDistinctFromZeroAndFinalizedIsReadOnly()
    {
        var repo = new UiCountRepository(); var vm = Counts(repo, Session()); await Open(vm);
        Assert.Equal("", vm.Lines[0].Quantity); Assert.Equal("Pendiente", vm.Lines[0].Difference);
        vm.Lines[0].Quantity = "0"; await vm.CaptureCommand.ExecuteAsync(null);
        Assert.Single(repo.Captures); Assert.Equal(0, repo.Captures[0].Lines[0].Quantity);
        Assert.Equal("0", vm.Lines[0].Quantity); Assert.Equal("-5", vm.Lines[0].Difference);
        vm.Reason = "Revisión física"; await vm.CloseCommand.ExecuteAsync(null); Assert.Equal(0, repo.Finalizations);
        vm.Confirmed = true; await vm.CloseCommand.ExecuteAsync(null);
        Assert.Equal(1, repo.Finalizations); Assert.False(vm.CanEdit);
        await vm.CloseCommand.ExecuteAsync(null); Assert.Equal(1, repo.Finalizations);
    }
    [Fact]
    public async Task CaptureChunksReloadHeaderVersionAndOnlySendsEditedLines()
    {
        var repo = new UiCountRepository(); repo.Lines.Clear();
        for (var i = 1; i <= 1001; i++) repo.Lines.Add(new() { Id = i, ProductId = i });
        var vm = Counts(repo, Session()); await Open(vm);
        foreach (var l in vm.Lines) l.Quantity = "0";
        await vm.CaptureCommand.ExecuteAsync(null);
        Assert.Equal(2, repo.Captures.Count); Assert.Equal(1000, repo.Captures[0].Lines.Count); Assert.Single(repo.Captures[1].Lines);
        Assert.NotEqual(repo.Captures[0].ExpectedVersion, repo.Captures[1].ExpectedVersion);
        await vm.CaptureCommand.ExecuteAsync(null); Assert.Equal(2, repo.Captures.Count);
    }
    [Fact]
    public async Task CaptureConflictReloadsWithoutRetryAndClosingRequiresSaving()
    {
        var repo = new UiCountRepository { RejectCapture = true }; var vm = Counts(repo, Session()); await Open(vm);
        vm.Lines[0].Quantity = "3"; vm.Reason = "Revisión física"; vm.Confirmed = true;
        await vm.CloseCommand.ExecuteAsync(null); Assert.Equal(0, repo.Finalizations);
        await vm.CaptureCommand.ExecuteAsync(null);
        Assert.Single(repo.Captures); Assert.Equal("", vm.Lines[0].Quantity); Assert.Contains("Otro equipo", vm.StatusMessage);
    }
    [Fact]
    public async Task LostCloseResponseDisablesWritesUntilExplicitReload()
    {
        var repo = new UiCountRepository { LoseResponse = true }; var vm = Counts(repo, Session()); await Open(vm);
        vm.Reason = "Cancelar por revisión"; vm.Confirmed = true;
        await vm.CancelCommand.ExecuteAsync(null);
        Assert.False(vm.CanEdit); Assert.Empty(vm.Lines); Assert.Equal(1, repo.Finalizations);
        await vm.CancelCommand.ExecuteAsync(null); Assert.Equal(1, repo.Finalizations);
        await Open(vm); Assert.False(vm.CanEdit); Assert.Contains("CANCELADO", vm.DetailLabel);
    }
    [Fact]
    public async Task LateReadCannotRestoreDataAfterSessionChange()
    {
        var repo = new UiCountRepository { PendingSearch = new() }; var session = Session(); var vm = Counts(repo, session);
        var load = vm.ReloadCommand.ExecuteAsync(null); Assert.True(vm.IsBusy);
        session.Clear(); repo.PendingSearch.SetResult(new([repo.Header], 1)); await load;
        Assert.Empty(vm.Items); Assert.False(vm.CanRead); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task RevokedReadClearsTheSessionAndDetail()
    {
        var repo = new UiCountRepository(); var session = Session(); var vm = Counts(repo, session); await Open(vm);
        repo.DenyRead = true; await vm.OpenCommand.ExecuteAsync(null);
        Assert.False(session.IsAuthenticated); Assert.Empty(vm.Lines);
    }
    [Fact]
    public async Task CountReaderCanCancelButCannotCloseWithoutAdjustPermission()
    {
        var repo = new UiCountRepository(); var vm = Counts(repo, Session("INVENTARIO.VER", "INVENTARIO.CONTEO")); await Open(vm);
        Assert.True(vm.CanEdit); Assert.False(vm.CanClose);
        vm.Reason = "Revisión física"; vm.Confirmed = true; await vm.CloseCommand.ExecuteAsync(null); Assert.Equal(0, repo.Finalizations);
        await vm.CancelCommand.ExecuteAsync(null); Assert.Equal(1, repo.Finalizations);
    }
    [Fact]
    public async Task CancellingSupplierEditorLeavesOriginalDataUnchanged()
    {
        var repo = new UiSupplierRepository(); var dialogs = new Dialogs(); var vm = Suppliers(repo, Session(), dialogs); await Open(vm);
        dialogs.Supplier = form => { form.Supplier.Name = "Cambio descartado"; form.Supplier.Active = false; return Task.FromResult(false); };
        await vm.EditSupplierCommand.ExecuteAsync(null);
        Assert.Equal("Proveedor de prueba", vm.Detail!.Name); Assert.True(vm.Detail.Active); Assert.Equal(0, repo.Writes);
    }
    [Fact]
    public async Task ContactMutationReloadsAggregateAndUsesLatestVersion()
    {
        var repo = new UiSupplierRepository(); var dialogs = new Dialogs(); var vm = Suppliers(repo, Session(), dialogs); await Open(vm);
        dialogs.Supplier = async form => { form.Contact.Name = "Nuevo contacto"; form.Reason = "Revisión contacto"; form.Confirmed = true; await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null); return true; };
        await vm.NewContactCommand.ExecuteAsync(null);
        Assert.Equal(1, repo.Writes); Assert.Equal(new byte[8], repo.LastContact!.ExpectedSupplierVersion); Assert.Equal(repo.Supplier.RowVersion, vm.Detail!.RowVersion);
        await vm.NewContactCommand.ExecuteAsync(null);
        Assert.Equal(2, repo.Writes); Assert.Equal(BitConverter.GetBytes(1L), repo.LastContact!.ExpectedSupplierVersion);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictOrLostSupplierResponseBlocksResubmissionFromStaleForm(bool conflict)
    {
        var repo = new UiSupplierRepository { Conflict = conflict, LoseResponse = !conflict }; var dialogs = new Dialogs(); var vm = Suppliers(repo, Session(), dialogs); await Open(vm);
        dialogs.Supplier = async form =>
        {
            form.Reason = "Cambio de ficha"; form.Confirmed = true;
            await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);
            Assert.False(form.CanEdit); Assert.False(form.CanSubmit);
            await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null);
            return false;
        };
        await vm.EditSupplierCommand.ExecuteAsync(null); Assert.Equal(1, repo.Writes);
    }
    [Fact]
    public async Task ReferencePreservesDecimalPrecisionAndVersion()
    {
        var repo = new UiSupplierRepository(); var dialogs = new Dialogs(); var vm = Suppliers(repo, Session(), dialogs); await Open(vm);
        vm.SelectedProduct = vm.References[0];
        dialogs.Supplier = async form => { form.Cost = "7.123456"; form.Minimum = "2.0001"; form.LeadTime = "10"; form.Reason = "Actualizar referencia"; form.Confirmed = true; await ((IAsyncRelayCommand)form.SaveCommand).ExecuteAsync(null); return true; };
        await vm.EditProductCommand.ExecuteAsync(null);
        Assert.Equal(7.123456m, repo.LastProduct!.Product.LastCost); Assert.Equal(2.0001m, repo.LastProduct.Product.MinimumQuantity); Assert.Equal((short?)10, repo.LastProduct.Product.LeadTimeDays);
    }
    [Fact]
    public async Task InactiveSupplierAndReaderCannotMutateChildren()
    {
        var repo = new UiSupplierRepository(); var dialogs = new Dialogs(); var session = Session("COMPRAS.VER"); var vm = Suppliers(repo, session, dialogs); await Open(vm);
        Assert.False(vm.CanManage); await vm.NewContactCommand.ExecuteAsync(null); Assert.Equal(0, dialogs.Opened);
        session.Begin(new(1, "prueba", "Prueba", 1, [], ["COMPRAS.VER", "PROVEEDORES.GESTIONAR"], false)); await Open(vm);
        vm.Detail!.Active = false; await vm.NewContactCommand.ExecuteAsync(null); Assert.Equal(0, dialogs.Opened);
    }
    [Fact]
    public async Task ProductSelectionSurvivesPagesAndValidationDoesNotTurnBlankCostIntoZero()
    {
        var picker = new ProductPickerViewModel(new(new CatalogRepository(), Session()));
        await picker.SearchCommand.ExecuteAsync(null); picker.Items[0].Selected = true;
        await picker.NextCommand.ExecuteAsync(null); picker.Items[0].Selected = true;
        Assert.Equal(2, picker.SelectedIds.Count);
        var form = new SupplierEditorDialogModel(SupplierEditorKind.Product, new(), null, new() { ProductId = 1 }, picker, [], _ => Task.FromResult(false));
        Assert.Null(form.BuildProduct()!.LastCost);
        form.Cost = "1,000"; Assert.Null(form.BuildProduct());
    }
    private sealed class Dialogs : IWorkflowDialogService
    {
        public int Opened { get; private set; }
        public Func<CountStartDialogModel, Task<bool>> Count { get; set; } = _ => Task.FromResult(false);
        public Func<SupplierEditorDialogModel, Task<bool>> Supplier { get; set; } = _ => Task.FromResult(false);
        public Task<bool> ShowCountStartAsync(CountStartDialogModel model) => Count(model);
        public Task<bool> ShowSupplierEditorAsync(SupplierEditorDialogModel model) { Opened++; return Supplier(model); }
    }
    private sealed class CatalogRepository : IInventoryRepository, IProductRepository
    {
        public Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int u, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([new() { Id = 1, Name = "Prueba" }]);
        public Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken ct = default) => Task.FromResult(new ProductPage([new() { Id = search.Page, Sku = "PRUEBA", Name = "Producto de prueba" }], 60));
        public Task<ProductCatalogs> GetCatalogsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<int>> SaveAsync(ProductData p, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<int>> CreateCategoryAsync(string n, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<InventorySnapshot> ReadAsync(int p, int w, int u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MovementPage> MovementsAsync(MovementFilter f, int u, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<long>> AdjustAsync(InventoryAdjustment r, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<long>> SetLotStateAsync(LotStateChange r, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
