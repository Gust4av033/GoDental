using System.Windows;
using System.Windows.Controls;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Auth;

public partial class AccessView : UserControl
{
    public AccessView() { InitializeComponent(); Unloaded += (_, _) => ClearPasswords(); IsVisibleChanged += (_, _) => { if (!IsVisible) ClearPasswords(); }; }
    private async void SubmitClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AccessViewModel viewModel) return;
        // Puente visual para PasswordBox: las claves no se enlazan a propiedades ni se guardan.
        try { await viewModel.SubmitAsync(PasswordInput.Password, ConfirmationInput.Password, CurrentPasswordInput.Password); }
        finally { ClearPasswords(); }
    }
    private void ClearPasswords() { PasswordInput.Clear(); ConfirmationInput.Clear(); CurrentPasswordInput.Clear(); }
}
