using System.Windows;
using GoDental.Application;
using GoDental.Infrastructure;
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
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplication();
                services.AddInfrastructure(context.Configuration);
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
