using System.Windows.Controls;
using System.Windows.Threading;

namespace GoDental.Wpf.Views.Auth;

public partial class UserCreateDialogView : UserControl
{
    public UserCreateDialogView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => UserNameInput.Focus()));
    }
    public (string Password, string Confirmation) ReadPasswords() => (NewPassword.Password, NewConfirmation.Password);
    public bool HasPasswords() => NewPassword.Password.Length > 0 || NewConfirmation.Password.Length > 0;
    public void ClearPasswords() { NewPassword.Clear(); NewConfirmation.Clear(); }
}
