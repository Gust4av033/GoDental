using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.ViewModels.Operations;
using GoDental.Application.Abstractions.Security;
using System.Globalization;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService navigation;
    private readonly ISettingsWindowService settingsWindow;
    public PurchasesViewModel? Purchases { get; }
    public bool IsPurchases => navigation.CurrentPage == SetupPage.Purchases;
    public bool CanOpenPurchases => Purchases?.CanRead == true;
    public CommercialParametersViewModel? CommercialParameters { get; }
    public HomeViewModel Home { get; }
    public ConnectionViewModel Connection { get; }
    public PendingOperationsViewModel? PendingOperations { get; }
    public bool IsPendingOperations => navigation.CurrentPage == SetupPage.PendingOperations;
    public AccessViewModel Access { get; }
    public ProductsViewModel Products { get; }
    public UsersViewModel Users { get; }
    public InventoryCountsViewModel Counts { get; }
    public SuppliersViewModel Suppliers { get; }
    public CustomersViewModel Customers { get; }
    public CashViewModel Cash { get; }
    public PosViewModel Pos { get; }
    public CreditViewModel Credit { get; }
    public OrdersViewModel Orders { get; }
    public CommercialHistoryViewModel CommercialHistory { get; }
    public bool IsCash => navigation.CurrentPage == SetupPage.Cash;
    public bool IsPos => navigation.CurrentPage == SetupPage.Pos;
    public bool IsCredit => navigation.CurrentPage == SetupPage.Credit;
    public bool IsOrders => navigation.CurrentPage == SetupPage.Orders;
    public bool IsSales => navigation.CurrentPage == SetupPage.Sales;
    public bool IsQuotations => navigation.CurrentPage == SetupPage.Quotations;
    public bool CanOpenCash => Cash.CanRead;
    public bool CanOpenPos => Pos.CanRead;
    public bool CanOpenCredit => Credit.CanRead;
    public bool CanOpenOrders => Orders.CanRead;
    public bool CanOpenSales => CommercialHistory.CanRead;
    private IEnumerable<MvpWorkspaceViewModel> MvpWorkspaces
    {
        get
        {
            if (Purchases is { } purchases) yield return purchases;
            if (CommercialParameters is { } parameters) yield return parameters;
            yield return Cash; yield return Pos; yield return Credit; yield return Orders; yield return CommercialHistory;
            if (Products.Inventory?.Barcodes is { } barcodes) yield return barcodes;
            if (Products.Inventory?.InitialInventory is { } initial) yield return initial;
        }
    }
    public bool HasPendingMvp => MvpWorkspaces.Any(m => m.Operation.HasPending || m.Operation.IsBusy);
    public bool CanCloseWorkspace => PendingOperations?.IsBusy != true && MvpWorkspaces.All(m => m.Operation.CanClose && !m.IsBusy && !m.IsEditing) && !Products.IsBusy && Products.Inventory?.IsBusy != true && Products.CategoryManager?.IsBusy != true && !Users.IsBusy && !Counts.IsBusy && !Suppliers.IsBusy && !Customers.IsBusy && !Access.IsSubmitting;
    public bool IsCustomers => navigation.CurrentPage == SetupPage.Customers;
    public bool CanOpenCustomers => Customers.CanRead;
    public bool IsCounts => navigation.CurrentPage == SetupPage.Counts;
    public bool IsSuppliers => navigation.CurrentPage == SetupPage.Suppliers;
    public bool CanOpenCounts => Counts.CanRead;
    public bool CanOpenSuppliers => Suppliers.CanRead;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    [NotifyPropertyChangedFor(nameof(IsSidebarExpanded))]
    private bool isSidebarCompact;
    public double SidebarWidth => IsSidebarCompact ? 64 : 184;
    public bool IsSidebarExpanded => !IsSidebarCompact;
    public string TodayLabel => DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-SV"));
    public string PageTitle => navigation.CurrentPage switch
    {
        SetupPage.Purchases => "Compras / Órdenes y recepciones",
        SetupPage.PendingOperations => "Operaciones pendientes",
        SetupPage.Cash => "Caja / Turnos y movimientos",
        SetupPage.Pos => "Ventas / Punto de venta",
        SetupPage.Credit => "Crédito / Cuentas y abonos",
        SetupPage.Orders => "Pedidos / Entregas",
        SetupPage.Sales => "Ventas / Comprobantes internos",
        SetupPage.Quotations => "Ventas / Cotizaciones",
        SetupPage.Connection => "Ajustes",
        SetupPage.Access => "Mi cuenta",
        SetupPage.Products => "Inventario / Productos",
        SetupPage.Counts => "Inventario / Conteos",
        SetupPage.Customers => "Clientes",
        SetupPage.Suppliers => "Compras / Proveedores",
        SetupPage.Users => "Seguridad / Usuarios",
        _ => "Inicio"
    };
    public bool IsHome => navigation.CurrentPage == SetupPage.Home;
    public bool IsConnection => navigation.CurrentPage == SetupPage.Connection;
    public bool IsAccess => navigation.CurrentPage == SetupPage.Access;
    public bool IsProducts => navigation.CurrentPage == SetupPage.Products;
    public bool IsUsers => navigation.CurrentPage == SetupPage.Users;
    public bool CanOpenUsers => Users.CanView;
    public bool CanOpenProducts => Products.CanView;
    public bool CanSignOut => CanCloseWorkspace && Access.IsAuthenticated && Products.IsNotBusy && !Access.IsBusy && !Users.IsBusy && !Counts.IsBusy && !Counts.IsEditing && !Suppliers.IsBusy && !Suppliers.IsEditing && !Customers.IsBusy && !Customers.IsEditing;

    public ShellViewModel(INavigationService navigation, ConnectionViewModel connection, AccessViewModel access, ProductsViewModel products, UsersViewModel users, ISessionController session, ISettingsWindowService settingsWindow, InventoryCountsViewModel counts, SuppliersViewModel suppliers, CustomersViewModel customers, CashViewModel cash, PosViewModel pos, CreditViewModel credit, OrdersViewModel orders, CommercialHistoryViewModel commercialHistory, HomeViewModel home, PendingOperationsViewModel? pendingOperations = null, CommercialParametersViewModel? commercialParameters = null, PurchasesViewModel? purchases = null)
    {
        this.navigation = navigation;
        Home = home;
        Purchases = purchases;
        CommercialParameters = commercialParameters;
        PendingOperations = pendingOperations;
        if (pendingOperations is not null) pendingOperations.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanCloseWorkspace)); OnPropertyChanged(nameof(CanSignOut)); };
        this.settingsWindow = settingsWindow;
        Connection = connection;
        Access = access; Products = products;
        Users = users; Counts = counts; Suppliers = suppliers; Customers = customers;
        Cash = cash; Pos = pos; Credit = credit; Orders = orders; CommercialHistory = commercialHistory;
        foreach (var model in MvpWorkspaces)
        {
            model.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
            model.Operation.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(HasPendingMvp)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        }
        customers.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        counts.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        suppliers.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        users.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        products.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        access.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(CanOpenPurchases));
            OnPropertyChanged(nameof(CanOpenProducts)); OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(CanOpenUsers));
            foreach (var n in new[] { nameof(CanOpenCash), nameof(CanOpenPos), nameof(CanOpenCredit), nameof(CanOpenOrders), nameof(CanOpenSales) }) OnPropertyChanged(n);
            OnPropertyChanged(nameof(CanOpenCounts)); OnPropertyChanged(nameof(CanOpenSuppliers)); OnPropertyChanged(nameof(CanOpenCustomers));
            var wasHome = IsHome;
            navigation.Navigate(session.IsAuthenticated ? SetupPage.Home : SetupPage.Access);
            if (session.IsAuthenticated && wasHome) Home.ReloadCommand.Execute(null);
            if (session.IsAuthenticated) Products.ReloadCommand.Execute(null);
        };
        navigation.PageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsPurchases));
            OnPropertyChanged(nameof(IsPendingOperations));
            foreach (var n in new[] { nameof(IsCash), nameof(IsPos), nameof(IsCredit), nameof(IsOrders), nameof(IsSales), nameof(IsQuotations) }) OnPropertyChanged(n);
            OnPropertyChanged(nameof(IsHome));
            if (IsHome) Home.ReloadCommand.Execute(null);
            OnPropertyChanged(nameof(IsConnection));
            OnPropertyChanged(nameof(IsAccess)); OnPropertyChanged(nameof(IsProducts));
            OnPropertyChanged(nameof(IsUsers));
            OnPropertyChanged(nameof(IsCounts)); OnPropertyChanged(nameof(IsSuppliers)); OnPropertyChanged(nameof(IsCustomers));
        };
    }

    [RelayCommand] private void Navigate(SetupPage page)
    {
        if (page == SetupPage.Purchases && !CanOpenPurchases) page = SetupPage.Access;
        if (page == SetupPage.Products && !CanOpenProducts) page = SetupPage.Access;
        if (page == SetupPage.Users && !CanOpenUsers) page = SetupPage.Access;
        if (page == SetupPage.Counts && !CanOpenCounts) page = SetupPage.Access;
        if (page == SetupPage.Suppliers && !CanOpenSuppliers) page = SetupPage.Access;
        if (page == SetupPage.Customers && !CanOpenCustomers) page = SetupPage.Access;
        if (page == SetupPage.Cash && !CanOpenCash || page == SetupPage.Pos && !CanOpenPos || page == SetupPage.Credit && !CanOpenCredit || page == SetupPage.Orders && !CanOpenOrders || page is SetupPage.Sales or SetupPage.Quotations && !CanOpenSales) page = SetupPage.Access;
        navigation.Navigate(page);
        if (page == SetupPage.Connection && CommercialParameters?.CanRead == true) CommercialParameters.ReloadCommand.Execute(null);
        if (page == SetupPage.Access && !Access.IsAuthenticated && !Access.IsBusy) Access.PrepareCommand.Execute(null);
        if (page == SetupPage.Purchases) Purchases?.ReloadCommand.Execute(null);
        if (page == SetupPage.PendingOperations) PendingOperations?.ReloadCommand.Execute(null);
        if (page == SetupPage.Cash) Cash.ReloadCommand.Execute(null);
        if (page == SetupPage.Pos && Pos.Boxes.Count == 0) Pos.ReloadCommand.Execute(null);
        if (page == SetupPage.Credit) Credit.ReloadCommand.Execute(null);
        if (page == SetupPage.Orders) Orders.ReloadCommand.Execute(null);
        if (page == SetupPage.Sales) CommercialHistory.ReloadSalesCommand.Execute(null);
        if (page == SetupPage.Quotations) CommercialHistory.ReloadQuotationsCommand.Execute(null);
        if (page == SetupPage.Products) Products.ReloadCommand.Execute(null);
        if (page == SetupPage.Users) Users.ReloadCommand.Execute(null);
        if (page == SetupPage.Counts) Counts.ReloadCommand.Execute(null);
        if (page == SetupPage.Suppliers) Suppliers.ReloadCommand.Execute(null);
        if (page == SetupPage.Customers) Customers.ReloadCommand.Execute(null);
    }
    [RelayCommand] private async Task OpenHomeOrderAsync(GoDental.Application.DTOs.OrderData? order)
    {
        if (!CanOpenOrders || Orders.IsBusy || order is null) return;
        await Orders.OpenOrderCommand.ExecuteAsync(order);
        if (Orders.Detail?.Order.Id == order.Id) navigation.Navigate(SetupPage.Orders);
    }
    [RelayCommand] private void ToggleSidebar() => IsSidebarCompact = !IsSidebarCompact;
    [RelayCommand] private void OpenSettings() => settingsWindow.Show();
}

