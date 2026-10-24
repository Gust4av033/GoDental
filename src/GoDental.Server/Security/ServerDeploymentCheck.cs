using System.Security.Principal;
using GoDental.Application.Abstractions.Persistence;
using Microsoft.Extensions.Options;

namespace GoDental.Server.Security;

public enum ServerDeploymentStatus { Ready, DevelopmentTlsUnverified, ConfigurationInvalid, WindowsRequired, ServiceIdentityDenied, TlsNotReady, SqlIdentityDenied, SqlNotReady }
public sealed class ServerDeploymentException(ServerDeploymentStatus status)
    : Exception("El servidor no está preparado. Código: " + status)
{
    public ServerDeploymentStatus Status { get; } = status;
}
public interface IServerDeploymentCheck
{
    Task<ServerDeploymentStatus> CheckAsync(CancellationToken ct = default);
}

public sealed class ServerDeploymentCheck(IOptions<ServerHostOptions> options, IHostEnvironment environment,
    IServerTlsCertificate tls, IDbConnectionFactory database) : IServerDeploymentCheck
{
    public async Task<ServerDeploymentStatus> CheckAsync(CancellationToken ct = default)
    {
        ServerHostOptions settings;
        try { settings = options.Value; }
        catch (OptionsValidationException) { return ServerDeploymentStatus.ConfigurationInvalid; }
        var development = environment.IsDevelopment();
        if (!ServerDeploymentPolicy.Valid(settings, development)) return ServerDeploymentStatus.ConfigurationInvalid;
        if (!development)
        {
            if (!OperatingSystem.IsWindows()) return ServerDeploymentStatus.WindowsRequired;
            using var identity = WindowsIdentity.GetCurrent();
            if (identity.User?.Value != settings.ServiceSid || identity.IsSystem || identity.IsAnonymous ||
                identity.User?.Value is "S-1-5-19" or "S-1-5-20" ||
                new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                return ServerDeploymentStatus.ServiceIdentityDenied;
        }
        if (!development || settings.HttpsCertificateThumbprint.Length != 0)
        {
            try { _ = tls.GetCertificate(); }
            catch (ServerDeploymentException) { return ServerDeploymentStatus.TlsNotReady; }
        }
        try
        {
            await using var connection = await database.OpenConnectionAsync(ct);
            // Kestrel resuelve el certificado del SDK en Development. No atribuir esa comprobación al diagnóstico local.
            return development && settings.HttpsCertificateThumbprint.Length == 0
                ? ServerDeploymentStatus.DevelopmentTlsUnverified : ServerDeploymentStatus.Ready;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException) { return ServerDeploymentStatus.SqlIdentityDenied; }
        catch (Exception) { return ServerDeploymentStatus.SqlNotReady; }
    }
}

public sealed class ServerStartupGate(IServerDeploymentCheck check, IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var status = await check.CheckAsync(cancellationToken);
        if (status != ServerDeploymentStatus.Ready &&
            !(status == ServerDeploymentStatus.DevelopmentTlsUnverified && environment.IsDevelopment()))
            throw new ServerDeploymentException(status);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
