using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.Navigation;
using GoDental.Application.Abstractions.Security;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService navigation;
    public ConnectionViewModel Connection { get; }
    public AccessViewModel Access { get; }
    public ProductsViewModel Products { get; }
    public UsersViewModel Users { get; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidebarWidth))]
    [NotifyPropertyChangedFor(nameof(IsSidebarExpanded))]
    private bool isSidebarCompact;
    public double SidebarWidth => IsSidebarCompact ? 72 : 228;
    public bool IsSidebarExpanded => !IsSidebarCompact;
    public string PageTitle => navigation.CurrentPage switch
    {
        SetupPage.Preparation => "Preparación del sistema",
        SetupPage.Connection => "Conexión y apariencia",
        SetupPage.Access => "Acceso de usuarios",
        SetupPage.Products => "Inventario / Productos",
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
    public bool CanSignOut => Access.IsAuthenticated && Products.IsNotBusy && !Access.IsBusy && !Users.IsBusy;

    public ShellViewModel(INavigationService navigation, ConnectionViewModel connection, AccessViewModel access, ProductsViewModel products, UsersViewModel users, ISessionController session)
    {
        this.navigation = navigation;
        Connection = connection;
        Access = access; Products = products;
        Users = users;
        users.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        products.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        access.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanSignOut));
        session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(CanOpenProducts)); OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(CanOpenUsers));
            navigation.Navigate(session.IsAuthenticated ? (Products.CanView ? SetupPage.Products : SetupPage.Home) : SetupPage.Access);
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
        };
    }

    [RelayCommand] private void Navigate(SetupPage page)
    {
        if (page == SetupPage.Products && !CanOpenProducts) page = SetupPage.Access;
        if (page == SetupPage.Users && !CanOpenUsers) page = SetupPage.Access;
        navigation.Navigate(page);
        if (page == SetupPage.Access && !Access.IsAuthenticated && !Access.IsBusy) Access.PrepareCommand.Execute(null);
        if (page == SetupPage.Products) Products.ReloadCommand.Execute(null);
        if (page == SetupPage.Users) Users.ReloadCommand.Execute(null);
    }
    [RelayCommand] private void ToggleSidebar() => IsSidebarCompact = !IsSidebarCompact;
}

