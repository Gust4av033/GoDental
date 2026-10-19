using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Auth;

public partial class UserManageDialogView : UserControl
{
    public UserManageDialogView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ReasonInput.Focus()));
    }
    public (string Password, string Confirmation) ReadPasswords() => (ResetPassword.Password, ResetConfirmation.Password);
    public bool HasPasswords() => ResetPassword.Password.Length > 0 || ResetConfirmation.Password.Length > 0;
    public void ClearPasswords() { ResetPassword.Clear(); ResetConfirmation.Clear(); }

    private async void ResetClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UsersViewModel manager) return;
        try { await manager.ResetPasswordAsync(ResetPassword.Password, ResetConfirmation.Password); }
        finally { ClearPasswords(); }
    }
}
