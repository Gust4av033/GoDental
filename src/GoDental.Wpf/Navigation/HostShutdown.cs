using Microsoft.Extensions.Hosting;

namespace GoDental.Wpf.Navigation;

public static class HostShutdown
{
    public static async Task StopAsync(IHost host)
    {
        try { await host.StopAsync(TimeSpan.FromSeconds(5)).WaitAsync(TimeSpan.FromSeconds(6)); }
        finally { host.Dispose(); }
    }
}
