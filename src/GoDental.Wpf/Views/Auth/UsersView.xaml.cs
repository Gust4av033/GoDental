using System.Windows;
using System.Windows.Controls;
using GoDental.Wpf.ViewModels;
namespace GoDental.Wpf.Views.Auth;
public partial class UsersView : UserControl
{
    public UsersView() { InitializeComponent(); Unloaded += (_, _) => ClearPasswords(); IsVisibleChanged += (_, _) => { if (!IsVisible) ClearPasswords(); }; }
    private async void CreateClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UsersViewModel vm) return;
        try { await vm.CreateAsync(NewPassword.Password, NewConfirmation.Password); } finally { ClearPasswords(); }
    }
    private async void ResetClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not UsersViewModel vm) return;
        try { await vm.ResetPasswordAsync(ResetPassword.Password, ResetConfirmation.Password); } finally { ClearPasswords(); }
    }
    private void ClearPasswords() { NewPassword.Clear(); NewConfirmation.Clear(); ResetPassword.Clear(); ResetConfirmation.Clear(); }
}
