using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.Navigation;
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
    public bool CanSignOut => Access.IsAuthenticated && Products.IsNotBusy && !Access.IsBusy && !Users.IsBusy && !Counts.IsBusy && !Counts.IsEditing && !Suppliers.IsBusy && !Suppliers.IsEditing && !Customers.IsBusy && !Customers.IsEditing;

    public ShellViewModel(INavigationService navigation, ConnectionViewModel connection, AccessViewModel access, ProductsViewModel products, UsersViewModel users, ISessionController session, ISettingsWindowService settingsWindow, InventoryCountsViewModel counts, SuppliersViewModel suppliers, CustomersViewModel customers)
    {
        this.navigation = navigation;
        this.settingsWindow = settingsWindow;
        Connection = connection;
        Access = access; Products = products;
        Users = users; Counts = counts; Suppliers = suppliers; Customers = customers;
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
            OnPropertyChanged(nameof(CanOpenCounts)); OnPropertyChanged(nameof(CanOpenSuppliers)); OnPropertyChanged(nameof(CanOpenCustomers));
            navigation.Navigate(session.IsAuthenticated ? SetupPage.Home : SetupPage.Access);
            if (session.IsAuthenticated) Products.ReloadCommand.Execute(null);
        };
        navigation.PageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PageTitle));
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
        navigation.Navigate(page);
        if (page == SetupPage.Access && !Access.IsAuthenticated && !Access.IsBusy) Access.PrepareCommand.Execute(null);
        if (page == SetupPage.Products) Products.ReloadCommand.Execute(null);
        if (page == SetupPage.Users) Users.ReloadCommand.Execute(null);
        if (page == SetupPage.Counts) Counts.ReloadCommand.Execute(null);
        if (page == SetupPage.Suppliers) Suppliers.ReloadCommand.Execute(null);
        if (page == SetupPage.Customers) Customers.ReloadCommand.Execute(null);
    }
    [RelayCommand] private void ToggleSidebar() => IsSidebarCompact = !IsSidebarCompact;
    [RelayCommand] private void OpenSettings() => settingsWindow.Show();
}

