using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Views.Auth;

public partial class AccessView : UserControl
{
    private AccessViewModel? observed;
    public AccessView()
    {
        InitializeComponent();
        Loaded += (_, _) => { ObserveModel(); Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFirstInput)); };
        Unloaded += (_, _) => { ObserveModel(null); ClearPasswords(); };
        DataContextChanged += (_, _) => { ClearPasswords(); ObserveModel(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) ClearPasswords(); };
    }
    private void ObserveModel() => ObserveModel(DataContext as AccessViewModel);
    private void ObserveModel(AccessViewModel? next)
    {
        if (ReferenceEquals(observed, next)) return;
        if (observed is not null) PropertyChangedEventManager.RemoveHandler(observed, AccessStateChanged, string.Empty);
        observed = next;
        if (observed is not null) PropertyChangedEventManager.AddHandler(observed, AccessStateChanged, string.Empty);
    }
    private void AccessStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AccessViewModel.MustChangePassword) or nameof(AccessViewModel.IsBootstrap) or nameof(AccessViewModel.IsAuthenticated))
        {
            ClearPasswords();
            if (IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusFirstInput));
        }
    }
    private void FocusFirstInput()
    {
        if (!IsVisible || !UserInput.IsEnabled) return;
        if (DataContext is AccessViewModel { MustChangePassword: true }) CurrentPasswordInput.FocusInput();
        else UserInput.Focus();
    }
    private void EntryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && UserInput.IsKeyboardFocusWithin && DataContext is AccessViewModel { IsBootstrap: false, MustChangePassword: false } && PasswordInput.Password.Length == 0)
        { PasswordInput.FocusInput(); e.Handled = true; }
    }
    private async void SubmitClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AccessViewModel { IsReady: true, IsBusy: false } viewModel) return;
        // Vaciar controles antes de IO; los strings sólo cruzan el gateway durante este intento.
        var password = PasswordInput.ReadAndClear();
        var confirmation = ConfirmationInput.ReadAndClear();
        var currentPassword = CurrentPasswordInput.ReadAndClear();
        try
        {
            await viewModel.SubmitAsync(password, confirmation, currentPassword);
            if (!viewModel.IsAuthenticated && IsVisible)
            {
                if (string.IsNullOrWhiteSpace(viewModel.UserName)) UserInput.Focus();
                else if (viewModel.MustChangePassword) CurrentPasswordInput.FocusInput();
                else PasswordInput.FocusInput();
            }
        }
        finally { ClearPasswords(); }
    }
    private void ClearPasswords() { PasswordInput.Clear(); ConfirmationInput.Clear(); CurrentPasswordInput.Clear(); }
}
