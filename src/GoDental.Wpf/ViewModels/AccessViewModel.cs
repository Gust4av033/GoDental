using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class AccessViewModel : ObservableObject
{
    private readonly IIdentityGateway gateway;
    private readonly ISessionController session;
    private readonly ILogger<AccessViewModel> logger;
    private UserIdentity? pendingIdentity;
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string statusMessage = "Guarda la conexión y comprueba la instalación para iniciar sesión.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle))] [NotifyPropertyChangedFor(nameof(NeedsConfirmation))] private bool isBootstrap;
    [ObservableProperty] private bool isReady;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle))] [NotifyPropertyChangedFor(nameof(NeedsConfirmation))] private bool mustChangePassword;
    public bool IsNotBusy => !IsBusy;
    public bool IsAuthenticated => session.IsAuthenticated;
    public string IdentityLabel => session.DisplayName ?? "Sin sesión de usuario";
    public bool NeedsConfirmation => IsBootstrap || MustChangePassword;
    public bool ShowSignIn => !session.IsAuthenticated;
    public string FormTitle => session.IsAuthenticated ? "Sesión iniciada" : MustChangePassword ? "Actualiza tu contraseña" : IsBootstrap ? "Crea el primer administrador" : "Inicia sesión";

    public AccessViewModel(IIdentityGateway gateway, ISessionController session, ILogger<AccessViewModel> logger)
    {
        this.gateway = gateway; this.session = session; this.logger = logger;
        session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsAuthenticated)); OnPropertyChanged(nameof(IdentityLabel)); OnPropertyChanged(nameof(FormTitle)); OnPropertyChanged(nameof(ShowSignIn));
            if (!session.IsAuthenticated) { IsReady = false; StatusMessage = "Tu sesión terminó. Comprueba el acceso para volver a ingresar."; }
        };
    }

    [RelayCommand]
    private async Task PrepareAsync()
    {
        if (IsBusy || IsAuthenticated) return;
        IsBusy = true; IsReady = false; MustChangePassword = false; pendingIdentity = null;
        try
        {
            IsBootstrap = await gateway.NeedsAdministratorAsync(); IsReady = true;
            StatusMessage = IsBootstrap
                ? "Elige tu usuario y una contraseña de al menos 10 caracteres. Esta cuenta administrará GoDental."
                : "Ingresa tus credenciales de GoDental.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    // Las contraseñas se reciben sólo durante el envío; no son propiedades persistentes del ViewModel.
    public async Task SubmitAsync(string password, string confirmation, string currentPassword)
    {
        if (IsBusy || !IsReady || IsAuthenticated) return;
        if (NeedsConfirmation && password != confirmation) { StatusMessage = "Las contraseñas no coinciden."; return; }
        IsBusy = true;
        try
        {
            if (MustChangePassword && pendingIdentity is not null)
            {
                var changed = await gateway.ChangePasswordAsync(pendingIdentity.Id, currentPassword, password);
                if (!changed.IsSuccess) { StatusMessage = changed.Message ?? "No se pudo actualizar la contraseña."; return; }
                UserName = pendingIdentity.UserName;
            }
            var result = IsBootstrap
                ? await gateway.CreateAdministratorAsync(UserName, DisplayName, password, Environment.MachineName)
                : await gateway.SignInAsync(UserName, password, Environment.MachineName);
            if (!result.IsSuccess || result.Value is null) { StatusMessage = result.Message ?? "No se pudo iniciar sesión."; return; }
            if (result.Value.MustChangePassword)
            {
                pendingIdentity = result.Value; MustChangePassword = true;
                StatusMessage = "Debes cambiar la contraseña temporal antes de acceder a los módulos.";
                return;
            }
            pendingIdentity = null; MustChangePassword = false; IsReady = false;
            session.Begin(result.Value); StatusMessage = "Sesión iniciada.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BeginPasswordChange()
    {
        if (IsBusy || !session.IsAuthenticated || session.UserId is null) return;
        var identity = new UserIdentity((int)session.UserId.Value, session.UserName!, session.DisplayName!, (int)session.WorkstationId!.Value, [], [], true);
        session.Clear(); pendingIdentity = identity; UserName = identity.UserName;
        IsBootstrap = false; MustChangePassword = true; IsReady = true;
        StatusMessage = "Confirma tu contraseña actual y elige una nueva. Volverás a iniciar sesión al terminar.";
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (session.UserId is { } id) await gateway.SignOutAsync(checked((int)id), (int?)session.WorkstationId);
        }
        catch (Exception ex) { logger.LogWarning("No se pudo auditar el cierre de sesión: {ErrorType}.", ex.GetType().Name); }
        finally
        {
            session.Clear(); pendingIdentity = null; IsReady = false; MustChangePassword = false; DisplayName = "";
            StatusMessage = "Sesión cerrada. Comprueba el acceso para volver a ingresar."; IsBusy = false;
        }
    }
    private void Fail(Exception ex)
    {
        logger.LogWarning("Falló el acceso de usuario: {ErrorType}.", ex.GetType().Name);
        StatusMessage = "No se pudo completar el acceso. Revisa la conexión guardada, la instalación y los permisos de SQL Server.";
    }
}
