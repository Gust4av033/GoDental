using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.Navigation;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService navigation;
    public ConnectionViewModel Connection { get; }
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
        _ => "Inicio"
    };
    public bool IsHome => navigation.CurrentPage == SetupPage.Home;
    public bool IsPreparation => navigation.CurrentPage == SetupPage.Preparation;
    public bool IsConnection => navigation.CurrentPage == SetupPage.Connection;

    public ShellViewModel(INavigationService navigation, ConnectionViewModel connection)
    {
        this.navigation = navigation;
        Connection = connection;
        navigation.PageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PageTitle));
            OnPropertyChanged(nameof(IsHome));
            OnPropertyChanged(nameof(IsPreparation));
            OnPropertyChanged(nameof(IsConnection));
        };
    }

    [RelayCommand] private void Navigate(SetupPage page) => navigation.Navigate(page);
    [RelayCommand] private void ToggleSidebar() => IsSidebarCompact = !IsSidebarCompact;
}
