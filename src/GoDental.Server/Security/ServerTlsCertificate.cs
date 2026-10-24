using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace GoDental.Server.Security;

public interface IServerTlsCertificate { X509Certificate2 GetCertificate(); }

public sealed class ServerTlsCertificate(IOptions<ServerHostOptions> options) : IServerTlsCertificate, IDisposable
{
    private X509Certificate2? _certificate;
    private readonly object _gate = new();

    public X509Certificate2 GetCertificate()
    {
        lock (_gate)
        {
            if (_certificate is not null) return _certificate;
            try
            {
                if (!OperatingSystem.IsWindows()) throw new ServerDeploymentException(ServerDeploymentStatus.WindowsRequired);
                var settings = options.Value;
                if (!ServerDeploymentPolicy.ValidThumbprint(settings.HttpsCertificateThumbprint) ||
                    !ServerDeploymentPolicy.TryUrl(settings.Url, out var url))
                    throw new ServerDeploymentException(ServerDeploymentStatus.ConfigurationInvalid);
                using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var all = store.Certificates;
                var matches = all.Find(X509FindType.FindByThumbprint, settings.HttpsCertificateThumbprint, false);
                try
                {
                    if (matches.Count != 1 || !ServerDeploymentPolicy.ValidCertificate(matches[0], url!.DnsSafeHost, DateTimeOffset.UtcNow))
                        throw new ServerDeploymentException(ServerDeploymentStatus.TlsNotReady);
                    using var chain = new X509Chain();
                    chain.ChainPolicy.DisableCertificateDownloads = true;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                    if (!chain.Build(matches[0])) throw new ServerDeploymentException(ServerDeploymentStatus.TlsNotReady);
                    _certificate = new X509Certificate2(matches[0]);
                    return _certificate;
                }
                finally
                {
                    foreach (var match in matches) match.Dispose();
                    foreach (var item in all) item.Dispose();
                }
            }
            catch (ServerDeploymentException) { throw; }
            catch (Exception) { throw new ServerDeploymentException(ServerDeploymentStatus.TlsNotReady); }
        }
    }

    public void Dispose() { lock (_gate) { _certificate?.Dispose(); _certificate = null; } }
}
