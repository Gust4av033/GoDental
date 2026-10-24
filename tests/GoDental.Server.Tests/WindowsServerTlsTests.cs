using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GoDental.Server;
using GoDental.Server.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GoDental.Server.Tests;

[SupportedOSPlatform("windows")]
public sealed class WindowsServerTlsTests
{
    [WindowsAdminFact]
    public async Task WindowsCertificateStoreCanServeRealTrustedHttpsWithExactHost()
    {
        using var fixture = await SyntheticTlsCertificate.CreateAsync();
        using var provider = fixture.Provider();
        var certificate = provider.GetCertificate();
        Assert.Equal(fixture.Thumbprint, certificate.Thumbprint);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate)));
        await using var app = builder.Build();
        app.MapGet("/", () => "SYNTHETIC_TLS_ONLY");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var port = new Uri(address).Port;
        // Validación normal del sistema: sin callbacks que omitan certificado/nombre/cadena.
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
        Assert.Equal("SYNTHETIC_TLS_ONLY", await client.GetStringAsync($"https://localhost:{port}/"));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetStringAsync($"https://127.0.0.1:{port}/"));
        await app.StopAsync();
    }

    [WindowsAdminFact]
    public async Task WindowsLoaderRejectsUntrustedChainMissingCertificateAndWrongHost()
    {
        using var fixture = await SyntheticTlsCertificate.CreateAsync(trustRoot: false);
        using (var missing = fixture.Provider(thumbprint: new string('0', 40)))
            Assert.Throws<ServerDeploymentException>(() => missing.GetCertificate());
        using (var wrong = fixture.Provider(url: "https://different.example:7443"))
            Assert.Throws<ServerDeploymentException>(() => wrong.GetCertificate());
        using var untrusted = fixture.Provider();
        Assert.Throws<ServerDeploymentException>(() => untrusted.GetCertificate());
    }

    // Certificados TLS sintéticos, no MH. Sólo se instala material público en Root; la clave
    // de la hoja va a Machine/My para ensayar el backend real y se retira junto con su key al salir.
    private sealed class SyntheticTlsCertificate : IDisposable
    {
        private readonly X509Certificate2 _authority;
        private readonly X509Certificate2 _leaf;
        public string Thumbprint => _leaf.Thumbprint;
        private SyntheticTlsCertificate(bool trustRoot)
        {
            var id = Guid.NewGuid().ToString("N");
            using var caKey = RSA.Create(2048);
            var authority = new CertificateRequest("CN=GoDental Synthetic " + id, caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            authority.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            authority.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            _authority = authority.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow.AddDays(30));
            using var leafKey = RSA.Create(2048);
            var leaf = new CertificateRequest("CN=GoDental Synthetic Leaf " + id, leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost");
            leaf.CertificateExtensions.Add(san.Build());
            leaf.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            leaf.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
            leaf.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            using var publicLeaf = leaf.Create(_authority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using var keyedLeaf = publicLeaf.CopyWithPrivateKey(leafKey);
            var pfx = keyedLeaf.Export(X509ContentType.Pfx);
            try { _leaf = new X509Certificate2(pfx, "", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet); }
            finally { CryptographicOperations.ZeroMemory(pfx); }
            // Root de usuario puede solicitar consentimiento UI aunque el runner esté elevado.
            // Root de máquina permite provisión administrativa no interactiva en este runner efímero.
            using var roots = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            roots.Open(OpenFlags.ReadWrite);
            using var publicRoot = new X509Certificate2(_authority.RawData);
            if (trustRoot) roots.Add(publicRoot);
            using var personal = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            personal.Open(OpenFlags.ReadWrite); personal.Add(_leaf);
        }
        public static async Task<SyntheticTlsCertificate> CreateAsync(bool trustRoot = true)
        {
            var fixture = new SyntheticTlsCertificate(trustRoot);
            if (!trustRoot) return fixture;
            try
            {
                // Windows propaga los cambios de Root de forma asíncrona. Preparar la CA antes
                // de invocar el selector real, sin callbacks ni flags que acepten una cadena inválida.
                X509ChainStatusFlags[] last = [];
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (!timeout.IsCancellationRequested)
                {
                    using var chain = new X509Chain();
                    chain.ChainPolicy.DisableCertificateDownloads = true;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                    if (chain.Build(fixture._leaf)) return fixture;
                    last = chain.ChainStatus.Select(status => status.Status).ToArray();
                    try { await Task.Delay(100, timeout.Token); }
                    catch (OperationCanceledException) { break; }
                }
                throw new InvalidOperationException("Confianza sintética Windows no preparada: " + string.Join(",", last));
            }
            catch { fixture.Dispose(); throw; }
        }
        public ServerTlsCertificate Provider(string? url = null, string? thumbprint = null) => new(Options.Create(new ServerHostOptions
            { Url = url ?? "https://localhost:7443", HttpsCertificateThumbprint = thumbprint ?? Thumbprint }));
        public void RemoveTrust()
        {
            using var roots = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            roots.Open(OpenFlags.ReadWrite); roots.Remove(_authority);
        }
        public void Dispose()
        {
            RemoveTrust();
            using var personal = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            personal.Open(OpenFlags.ReadWrite); personal.Remove(_leaf);
            using var key = _leaf.GetRSAPrivateKey();
            if (key is RSACng cng) cng.Key.Delete();
            else if (key is RSACryptoServiceProvider csp) csp.PersistKeyInCsp = false;
            _leaf.Dispose(); _authority.Dispose();
        }
    }
}
