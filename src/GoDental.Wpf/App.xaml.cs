using System.IO;
using System.Windows;
using System.Windows.Threading;
using GoDental.Application;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Infrastructure;
using GoDental.Infrastructure.Data;
using GoDental.Wpf.Common;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.Navigation;
using GoDental.Wpf.Themes;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnFatalException;
        try
        {
            var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoDental");
            _host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.SetBasePath(AppContext.BaseDirectory);
                    configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddDebug();
                    logging.AddProvider(new LocalFileLoggerProvider(Path.Combine(localFolder, "logs", "application.log")));
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddApplication();
                    services.AddInfrastructure(context.Configuration);
                    services.AddSingleton<IWorkstationSettingsStore>(new LocalWorkstationSettingsStore(Path.Combine(localFolder, "workstation.json")));
                    services.AddSingleton<System.Windows.Application>(this);
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<ConnectionViewModel>();
                    services.AddSingleton<AccessViewModel>();
                    services.AddSingleton<InventoryViewModel>();
                    services.AddSingleton<CategoriesViewModel>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<ProductEditorFactory>();
                    services.AddSingleton<ProductsViewModel>();
                    services.AddSingleton<UsersViewModel>();
                    services.AddSingleton<ShellViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .UseDefaultServiceProvider(options => { options.ValidateOnBuild = true; options.ValidateScopes = true; })
                .Build();

            await _host.StartAsync();
            await _host.Services.GetRequiredService<ConnectionViewModel>().InitializeAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            _host.Services.GetRequiredService<ILogger<App>>().LogInformation("Inicio de GoDental 0.3.0; inventario operativo.");
        }
        catch (Exception ex)
        {
            _host?.Services.GetService<ILogger<App>>()?.LogCritical("No se pudo iniciar: {ErrorType}.", ex.GetType().Name);
            MessageBox.Show("No se pudo iniciar GoDental. Revisa el registro local de la aplicación.", "GoDental", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _host?.Services.GetService<ILogger<App>>()?.LogCritical("Error de interfaz: {ErrorType}.", e.Exception.GetType().Name);
        MessageBox.Show("Ocurrió un error inesperado. GoDental se cerrará; puedes volver a abrirlo.", "GoDental", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _host?.Services.GetService<ILogger<App>>()?.LogError("Tarea no observada: {ErrorType}.", e.Exception.GetType().Name);
        e.SetObserved();
    }

    private void OnFatalException(object sender, UnhandledExceptionEventArgs e) =>
        _host?.Services.GetService<ILogger<App>>()?.LogCritical("Error fatal: {ErrorType}.", e.ExceptionObject.GetType().Name);

    protected override async void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnFatalException;
        if (_host is not null)
        {
            _host.Services.GetRequiredService<ConnectionViewModel>().CheckConnectionCommand.Cancel();
            try { await _host.StopAsync(TimeSpan.FromSeconds(5)); }
            finally { _host.Dispose(); }
        }

        base.OnExit(e);
    }
}

