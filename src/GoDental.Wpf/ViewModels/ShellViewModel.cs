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
    public ConnectionViewModel Connection { get; }
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
    public bool HasPendingMvp => new MvpWorkspaceViewModel[] { Cash, Pos, Credit, Orders, CommercialHistory }.Any(m => m.Operation.HasPending || m.Operation.IsBusy);
    public bool CanCloseWorkspace => !HasPendingMvp && new MvpWorkspaceViewModel[] { Cash, Pos, Credit, Orders, CommercialHistory }.All(m => !m.IsBusy && !m.IsEditing);
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
    public double SidebarWidth => IsSidebarCompact ? 64 : 208;
    public bool IsSidebarExpanded => !IsSidebarCompact;
    public string TodayLabel => DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-SV"));
    public string PageTitle => navigation.CurrentPage switch
    {
        SetupPage.Cash => "Caja / Turnos y movimientos",
        SetupPage.Pos => "Ventas / Punto de venta",
        SetupPage.Credit => "Crédito / Cuentas y abonos",
        SetupPage.Orders => "Pedidos / Entregas",
        SetupPage.Sales => "Ventas / Comprobantes internos",
        SetupPage.Quotations => "Ventas / Cotizaciones",
        SetupPage.Preparation => "Preparación del sistema",
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
    public bool IsPreparation => navigation.CurrentPage == SetupPage.Preparation;
    public bool IsConnection => navigation.CurrentPage == SetupPage.Connection;
    public bool IsAccess => navigation.CurrentPage == SetupPage.Access;
    public bool IsProducts => navigation.CurrentPage == SetupPage.Products;
    public bool IsUsers => navigation.CurrentPage == SetupPage.Users;
    public bool CanOpenUsers => Users.CanView;
    public bool CanOpenProducts => Products.CanView;
    public bool CanSignOut => CanCloseWorkspace && Access.IsAuthenticated && Products.IsNotBusy && !Access.IsBusy && !Users.IsBusy && !Counts.IsBusy && !Counts.IsEditing && !Suppliers.IsBusy && !Suppliers.IsEditing && !Customers.IsBusy && !Customers.IsEditing;

    public ShellViewModel(INavigationService navigation, ConnectionViewModel connection, AccessViewModel access, ProductsViewModel products, UsersViewModel users, ISessionController session, ISettingsWindowService settingsWindow, InventoryCountsViewModel counts, SuppliersViewModel suppliers, CustomersViewModel customers, CashViewModel cash, PosViewModel pos, CreditViewModel credit, OrdersViewModel orders, CommercialHistoryViewModel commercialHistory)
    {
        this.navigation = navigation;
        this.settingsWindow = settingsWindow;
        Connection = connection;
        Access = access; Products = products;
        Users = users; Counts = counts; Suppliers = suppliers; Customers = customers;
        Cash = cash; Pos = pos; Credit = credit; Orders = orders; CommercialHistory = commercialHistory;
        foreach (var model in new MvpWorkspaceViewModel[] { cash, pos, credit, orders, commercialHistory })
        {
            model.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
            model.Operation.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanSignOut)); OnPropertyChanged(nameof(HasPendingMvp)); OnPropertyChanged(nameof(CanCloseWorkspace)); };
        }
        customers.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        counts.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        suppliers.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        users.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        products.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        access.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(CanOpenProducts)); OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(CanOpenUsers));
            foreach (var n in new[] { nameof(CanOpenCash), nameof(CanOpenPos), nameof(CanOpenCredit), nameof(CanOpenOrders), nameof(CanOpenSales) }) OnPropertyChanged(n);
            OnPropertyChanged(nameof(CanOpenCounts)); OnPropertyChanged(nameof(CanOpenSuppliers)); OnPropertyChanged(nameof(CanOpenCustomers));
            navigation.Navigate(session.IsAuthenticated ? SetupPage.Home : SetupPage.Access);
            if (session.IsAuthenticated) Products.ReloadCommand.Execute(null);
        };
        navigation.PageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PageTitle));
            foreach (var n in new[] { nameof(IsCash), nameof(IsPos), nameof(IsCredit), nameof(IsOrders), nameof(IsSales), nameof(IsQuotations) }) OnPropertyChanged(n);
            OnPropertyChanged(nameof(IsHome));
            OnPropertyChanged(nameof(IsPreparation));
            OnPropertyChanged(nameof(IsConnection));
            OnPropertyChanged(nameof(IsAccess)); OnPropertyChanged(nameof(IsProducts));
            OnPropertyChanged(nameof(IsUsers));
            OnPropertyChanged(nameof(IsCounts)); OnPropertyChanged(nameof(IsSuppliers)); OnPropertyChanged(nameof(IsCustomers));
        };
    }

    [RelayCommand] private void Navigate(SetupPage page)
    {
        if (page == SetupPage.Products && !CanOpenProducts) page = SetupPage.Access;
        if (page == SetupPage.Users && !CanOpenUsers) page = SetupPage.Access;
        if (page == SetupPage.Counts && !CanOpenCounts) page = SetupPage.Access;
        if (page == SetupPage.Suppliers && !CanOpenSuppliers) page = SetupPage.Access;
        if (page == SetupPage.Customers && !CanOpenCustomers) page = SetupPage.Access;
        if (page == SetupPage.Cash && !CanOpenCash || page == SetupPage.Pos && !CanOpenPos || page == SetupPage.Credit && !CanOpenCredit || page == SetupPage.Orders && !CanOpenOrders || page is SetupPage.Sales or SetupPage.Quotations && !CanOpenSales) page = SetupPage.Access;
        navigation.Navigate(page);
        if (page == SetupPage.Access && !Access.IsAuthenticated && !Access.IsBusy) Access.PrepareCommand.Execute(null);
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
    [RelayCommand] private void ToggleSidebar() => IsSidebarCompact = !IsSidebarCompact;
    [RelayCommand] private void OpenSettings() => settingsWindow.Show();
}

