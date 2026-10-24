using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.Validators;
using GoDental.Application.Abstractions.Security;
using GoDental.Wpf.Themes;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ConnectionViewModel : ObservableObject
{
    private readonly IWorkstationSettingsStore store;
    private readonly IConnectionHealthCheck healthCheck;
    private readonly IThemeService themes;
    private readonly ILogger<ConnectionViewModel> logger;
    private readonly ISessionController? session;

    public ConnectionViewModel(IWorkstationSettingsStore store, IConnectionHealthCheck healthCheck,
        IThemeService themes, ILogger<ConnectionViewModel> logger, ISessionController? session = null)
    {
        this.store = store; this.healthCheck = healthCheck; this.themes = themes; this.logger = logger; this.session = session;
        if (session is not null) session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(CanEditConnection)); OnPropertyChanged(nameof(ConnectionEditHint));
        };
    }
    private readonly SemaphoreSlim settingsWriteGate = new(1, 1);
    [ObservableProperty] private string server = "";
    [ObservableProperty] private string database = "";
    [ObservableProperty] private string statusMessage = "Configura la conexión para comprobar el acceso al servidor.";
    [ObservableProperty] private string connectionStatus = "Sin comprobar";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))] [NotifyPropertyChangedFor(nameof(CanEditConnection))] private bool isBusy;
    [ObservableProperty] private bool isDark;
    [ObservableProperty] private string themePreferenceStatus = "";
    [ObservableProperty] private bool localDevelopment;
    public bool IsNotBusy => !IsBusy;
    public bool CanEditConnection => IsNotBusy && session?.IsAuthenticated != true;
    public string ConnectionEditHint => session?.IsAuthenticated == true
        ? "Conexión protegida: cierra sesión para cambiar el servidor o la base de datos."
        : "Configura dónde se guardan los datos de tu depósito. Los cambios se aplican a este equipo.";
    public string WorkstationName => Environment.MachineName;

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await store.LoadAsync();
            Server = settings.Server;
            Database = settings.Database;
            IsDark = settings.Theme == "Dark";
            LocalDevelopment = settings.LocalDevelopment;
            themes.Apply(settings.Theme);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("No se pudo leer el perfil local: {ErrorType}.", ex.GetType().Name);
            StatusMessage = "No se pudo leer la configuración guardada. Puedes volver a configurarla y guardarla.";
        }
    }

    partial void OnServerChanged(string value) => InvalidateConnection();
    partial void OnDatabaseChanged(string value) => InvalidateConnection();
    partial void OnLocalDevelopmentChanged(bool value) => InvalidateConnection();
    partial void OnIsDarkChanged(bool value) => themes.Apply(value ? "Dark" : "Light");

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SaveThemePreferenceAsync()
    {
        ThemePreferenceStatus = "Guardando tema…";
        await settingsWriteGate.WaitAsync();
        try
        {
            var saved = await store.LoadAsync();
            await store.SaveAsync(saved with { Theme = IsDark ? "Dark" : "Light" });
            ThemePreferenceStatus = "Tema guardado en este equipo.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning("No se pudo conservar el tema: {ErrorType}.", ex.GetType().Name);
            StatusMessage = "El tema se aplicó, pero no se pudo guardar en este equipo.";
            ThemePreferenceStatus = "No se pudo guardar el tema.";
        }
        finally { settingsWriteGate.Release(); }
    }
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SetThemeAsync(string? theme)
    {
        if (theme is not ("Light" or "Dark")) return;
        IsDark = theme == "Dark";
        await SaveThemePreferenceAsync();
    }

    private void InvalidateConnection()
    {
        ConnectionStatus = "Sin comprobar";
        StatusMessage = "La configuración cambió. Comprueba la conexión antes de continuar.";
    }
    private WorkstationSettings Snapshot() => new(Server.Trim(), Database.Trim(), IsDark ? "Dark" : "Light", LocalDevelopment);
    private bool CanOperate() => !IsBusy;
    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CheckConnectionCommand.NotifyCanExecuteChanged();
        UseLocalDevelopmentCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void UseLocalDevelopment()
    {
        if (IsBusy) return;
        if (session?.IsAuthenticated == true) { StatusMessage = "Cierra sesión antes de cambiar de base."; return; }
        Server = "localhost"; Database = "GoDental_Development"; LocalDevelopment = true;
        StatusMessage = "Perfil de desarrollo local seleccionado. Guarda la configuración antes de entrar.";
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task SaveAsync()
    {
        if (session?.IsAuthenticated == true) { StatusMessage = "Cierra sesión antes de guardar cambios de conexión."; return; }
        var snapshot = Snapshot();
        if (WorkstationSettingsValidator.Validate(snapshot) is { } error)
        {
            StatusMessage = error;
            return;
        }
        IsBusy = true;
        try
        {
            await settingsWriteGate.WaitAsync();
            try { await store.SaveAsync(snapshot with { Theme = IsDark ? "Dark" : "Light" }); }
            finally { settingsWriteGate.Release(); }
            StatusMessage = "Configuración guardada en este equipo.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("No se pudo guardar el perfil local: {ErrorType}.", ex.GetType().Name);
            StatusMessage = "No se pudo guardar. Revisa los permisos de tu perfil de Windows.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanOperate), IncludeCancelCommand = true)]
    private async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        var snapshot = Snapshot();
        if (WorkstationSettingsValidator.Validate(snapshot) is { } error)
        {
            StatusMessage = error;
            return;
        }
        IsBusy = true;
        ConnectionStatus = "Comprobando…";
        StatusMessage = "Comprobando el acceso a SQL Server…";
        try
        {
            var result = await healthCheck.CheckAsync(snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ConnectionStatus = result.IsSuccess ? "Conexión comprobada" : "Sin conexión";
            StatusMessage = result.Message;
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus = "Sin comprobar";
            StatusMessage = "Comprobación cancelada.";
        }
        catch (Exception ex)
        {
            logger.LogError("Falló la comprobación: {ErrorType}.", ex.GetType().Name);
            ConnectionStatus = "Sin conexión";
            StatusMessage = "No se pudo completar la comprobación. Puedes volver a intentarlo.";
        }
        finally { IsBusy = false; }
    }
}
