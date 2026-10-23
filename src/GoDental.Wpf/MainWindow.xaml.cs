using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf;

public partial class MainWindow : Window
{
    private DispatcherOperation? scrollUpdate;
    private bool resetPageScroll;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        PropertyChangedEventManager.AddHandler(viewModel, OnNavigationLayoutChanged, nameof(ShellViewModel.PageTitle));
        PropertyChangedEventManager.AddHandler(viewModel, OnNavigationLayoutChanged, nameof(ShellViewModel.IsSidebarCompact));
        Loaded += (_, _) => QueueScrollUpdate(resetPage: true);
        SidebarScroll.SizeChanged += (_, _) => QueueScrollUpdate(resetPage: false);
        Closed += (_, _) =>
        {
            PropertyChangedEventManager.RemoveHandler(viewModel, OnNavigationLayoutChanged, nameof(ShellViewModel.PageTitle));
            PropertyChangedEventManager.RemoveHandler(viewModel, OnNavigationLayoutChanged, nameof(ShellViewModel.IsSidebarCompact));
            scrollUpdate?.Abort();
        };
        Closing += (_, e) =>
        {
            if (viewModel.CanCloseWorkspace) return;
            e.Cancel = true;
            MessageBox.Show(this, "Hay una operación en curso o pendiente de confirmar. Vuelve a su página y consulta el estado antes de cerrar GoDental.", "GoDental", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
        MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
        Width = Math.Min(Width, SystemParameters.WorkArea.Width);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
    }

    private void OnNavigationLayoutChanged(object? sender, PropertyChangedEventArgs e) =>
        QueueScrollUpdate(resetPage: e.PropertyName == nameof(ShellViewModel.PageTitle));

    // Comportamiento visual: esperar a que los enlaces de visibilidad y el layout se actualicen.
    private void QueueScrollUpdate(bool resetPage)
    {
        resetPageScroll |= resetPage;
        if (scrollUpdate?.Status == DispatcherOperationStatus.Pending) return;
        scrollUpdate = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            PageHost.UpdateLayout();
            if (resetPageScroll) PageScroll.ScrollToHome();
            resetPageScroll = false;
            SidebarLinks.Children.OfType<RadioButton>()
                .FirstOrDefault(button => button.IsChecked == true && button.IsVisible)?.BringIntoView();
        }));
    }
}
