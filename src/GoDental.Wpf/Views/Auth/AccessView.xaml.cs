using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Auth;

public partial class AccessView : UserControl
{
    public AccessView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (UserInput.IsVisible && UserInput.IsEnabled) UserInput.Focus(); }));
        Unloaded += (_, _) => ClearPasswords();
        IsVisibleChanged += (_, _) => { if (!IsVisible) ClearPasswords(); };
    }
    private async void SubmitClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AccessViewModel viewModel) return;
        // Puente visual para PasswordBox: las claves no se enlazan a propiedades ni se guardan.
        try { await viewModel.SubmitAsync(PasswordInput.Password, ConfirmationInput.Password, CurrentPasswordInput.Password); }
        finally { ClearPasswords(); }
    }
    private void ClearPasswords() { PasswordInput.Clear(); ConfirmationInput.Clear(); CurrentPasswordInput.Clear(); }
}
