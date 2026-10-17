using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;
namespace GoDental.Wpf.ViewModels;
public sealed partial class UsersViewModel : ObservableObject
{
    private readonly UserManagementService service;
    private readonly ISessionController session;
    private readonly ILogger<UsersViewModel> logger;
    public ObservableCollection<UserSummary> Users { get; } = [];
    public ObservableCollection<CatalogOption> Roles { get; } = [];
    [ObservableProperty] private UserSummary? selectedUser;
    [ObservableProperty] private CatalogOption? role;
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private string statusMessage = "Cada usuario debe tener su propia cuenta.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] [NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    public bool CanView => service.CanRead;
    public bool CanManage => service.CanManage;
    public bool CanEdit => CanManage && !IsBusy;
    public bool IsNotBusy => !IsBusy;
    public UsersViewModel(UserManagementService service, ISessionController session, ILogger<UsersViewModel> logger)
    {
        this.service = service; this.session = session; this.logger = logger;
        session.Changed += (_, _) =>
        {
            Users.Clear(); Roles.Clear(); SelectedUser = null; UserName = ""; DisplayName = ""; Reason = "";
            OnPropertyChanged(nameof(CanView)); OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanManage));
        };
    }
    [RelayCommand] private async Task ReloadAsync()
    {
        if (IsBusy || !CanView) return;
        IsBusy = true; var revision = session.Revision;
        try
        {
            var result = await service.ListAsync(); var roles = await service.RolesAsync();
            if (revision != session.Revision) return;
            if (!result.IsSuccess) { StatusMessage = result.Message ?? "No se pudieron consultar usuarios."; return; }
            Users.Clear(); foreach (var user in result.Value!) Users.Add(user);
            Roles.Clear(); foreach (var r in roles) Roles.Add(r);
            Role = Roles.FirstOrDefault(r => r.Name == "Vendedor") ?? Roles.FirstOrDefault();
            SelectedUser = null; StatusMessage = $"{Users.Count} usuarios. Los cambios de estado y contraseña quedan auditados.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }
    public async Task CreateAsync(string password, string confirmation)
    {
        if (!CanEdit) return;
        if (password != confirmation) { StatusMessage = "Las contraseñas no coinciden."; return; }
        await MutateAsync(() => service.CreateAsync(UserName, DisplayName, Role?.Id ?? 0, password), "Usuario creado. Deberá cambiar la contraseña temporal al ingresar.");
    }
    public async Task ResetPasswordAsync(string password, string confirmation)
    {
        if (!CanEdit) return;
        if (SelectedUser is null) { StatusMessage = "Selecciona la cuenta que deseas restablecer."; return; }
        if (password != confirmation) { StatusMessage = "Las contraseñas no coinciden."; return; }
        var target = SelectedUser;
        await MutateAsync(() => service.ResetPasswordAsync(target, password, Reason), "Contraseña restablecida. El usuario deberá cambiarla al ingresar.");
    }
    [RelayCommand] private async Task ToggleActiveAsync()
    {
        if (!CanEdit) return;
        if (SelectedUser is null) { StatusMessage = "Selecciona una cuenta."; return; }
        var target = SelectedUser;
        await MutateAsync(() => service.SetActiveAsync(target, !target.Active, Reason), target.Active ? "Usuario desactivado." : "Usuario reactivado.");
    }
    private async Task MutateAsync(Func<Task<Result>> mutation, string success)
    {
        IsBusy = true; var revision = session.Revision;
        try
        {
            var result = await mutation(); if (revision != session.Revision) return;
            if (!result.IsSuccess) { StatusMessage = result.Message ?? "No se pudo guardar."; return; }
            UserName = ""; DisplayName = ""; Reason = ""; IsBusy = false; await ReloadAsync(); StatusMessage = success;
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }
    private void Fail(Exception ex)
    {
        logger.LogWarning("Falló la administración de usuarios: {ErrorType}.", ex.GetType().Name);
        if (ex is UnauthorizedAccessException) { session.Clear(); return; }
        StatusMessage = "No se pudo completar la operación. Revisa la conexión y actualiza el listado antes de repetir un cambio.";
    }
}
