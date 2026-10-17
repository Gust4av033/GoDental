using CommunityToolkit.Mvvm.ComponentModel;

namespace GoDental.Wpf.Navigation;

public enum SetupPage { Home, Preparation, Connection }

public interface INavigationService
{
    SetupPage CurrentPage { get; }
    void Navigate(SetupPage page);
    event EventHandler? PageChanged;
}

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    [ObservableProperty] private SetupPage currentPage;
    public event EventHandler? PageChanged;
    public void Navigate(SetupPage page)
    {
        if (CurrentPage == page) return;
        CurrentPage = page;
        PageChanged?.Invoke(this, EventArgs.Empty);
    }
}
