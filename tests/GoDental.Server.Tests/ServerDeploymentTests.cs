using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Server;
using GoDental.Server.Security;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GoDental.Server.Tests;

public sealed class ServerDeploymentTests
{
    [Theory]
    [InlineData("https://localhost:7443", true)]
    [InlineData("https://127.0.0.1:7443", true)]
    [InlineData("https://[::1]:7443", true)]
    [InlineData("https://servidor.example:7443", true)]
    [InlineData("http://localhost:7443", false)]
    [InlineData("https://user:password@localhost", false)]
    [InlineData("https://localhost?token=test", false)]
    [InlineData("https://localhost/#test", false)]
    [InlineData("https://localhost/api", false)]
    [InlineData("https://localhost:0", false)]
    [InlineData("https://*:7443", false)]
    [InlineData("", false)]
    public void OnlyOneConcreteHttpsRootIsAccepted(string value, bool accepted) =>
        Assert.Equal(accepted, ServerDeploymentPolicy.TryUrl(value, out _));

    [Fact]
    public void DevelopmentCannotExposeLanOrRelaxSqlCertificateForProduction()
    {
        Assert.True(ServerDeploymentPolicy.Valid(Settings(), true));
        Assert.False(ServerDeploymentPolicy.Valid(Settings("https://lan.example:7443"), true));
        Assert.False(ServerDeploymentPolicy.Valid(Settings(), false));
        Assert.False(ServerDeploymentPolicy.Valid(new() { Url = "https://localhost", SqlServer = "localhost",
            Database = "GoDental_Development", ServiceSid = "synthetic", HttpsCertificateThumbprint = "not-a-thumbprint" }, false));
    }

    [Theory]
    [InlineData(ServerDeploymentStatus.SqlNotReady)]
    [InlineData(ServerDeploymentStatus.SqlIdentityDenied)]
    [InlineData(ServerDeploymentStatus.ServiceIdentityDenied)]
    [InlineData(ServerDeploymentStatus.TlsNotReady)]
    public async Task StartupGateBlocksEveryUnreadyDeployment(ServerDeploymentStatus failure)
    {
        var gate = new ServerStartupGate(new SyntheticStartupCheck(() => failure), new SyntheticEnvironment("Production"));
        var error = await Assert.ThrowsAsync<ServerDeploymentException>(() => gate.StartAsync(CancellationToken.None));
        Assert.Equal(failure, error.Status);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task KestrelDoesNotListenBeforeStartupGateAndCannotStartOnSqlFailure()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start(); var port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
        var checkedBeforeListening = false;
        builder.Services.AddSingleton<IServerDeploymentCheck>(new SyntheticStartupCheck(() =>
        {
            using var probe = new TcpClient();
            Assert.Throws<SocketException>(() => probe.Connect(IPAddress.Loopback, port));
            checkedBeforeListening = true;
            return ServerDeploymentStatus.SqlIdentityDenied;
        }));
        builder.Services.AddHostedService<ServerStartupGate>();
        await using var app = builder.Build();
        var error = await Assert.ThrowsAsync<ServerDeploymentException>(() => app.StartAsync());
        Assert.Equal(ServerDeploymentStatus.SqlIdentityDenied, error.Status);
        Assert.True(checkedBeforeListening);
        using var after = new TcpClient();
        await Assert.ThrowsAsync<SocketException>(async () => await after.ConnectAsync(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task DevelopmentCertificateDeferralCannotAuthorizeProductionStartup()
    {
        var gate = new ServerStartupGate(new SyntheticStartupCheck(() => ServerDeploymentStatus.DevelopmentTlsUnverified),
            new SyntheticEnvironment("Production"));
        await Assert.ThrowsAsync<ServerDeploymentException>(() => gate.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SqlProbeSanitizesProviderFailureAndDoesNotUseTlsFallback()
    {
        var database = new ThrowingDatabase(new InvalidOperationException("SYNTHETIC_PRIVATE_CONNECTION_STRING"));
        var check = new ServerDeploymentCheck(Options.Create(Settings()), new SyntheticEnvironment("Development"), new NeverTls(), database);
        Assert.Equal(ServerDeploymentStatus.SqlNotReady, await check.CheckAsync());
        Assert.Equal(1, database.Calls);
        Assert.DoesNotContain("SYNTHETIC_PRIVATE", new ServerDeploymentException(ServerDeploymentStatus.SqlNotReady).ToString());
    }

    [Fact]
    public async Task SqlPrivilegeFailureAndCancellationAreDistinct()
    {
        var database = new ThrowingDatabase(new UnauthorizedAccessException("SYNTHETIC_PRIVATE_SQL_DETAIL"));
        var check = new ServerDeploymentCheck(Options.Create(Settings()), new SyntheticEnvironment("Development"), new NeverTls(), database);
        Assert.Equal(ServerDeploymentStatus.SqlIdentityDenied, await check.CheckAsync());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.CheckAsync(cancelled.Token));
    }

    [Fact]
    public async Task ProductionCannotUseDevelopmentCertificateOrCallerSidToReachSql()
    {
        var database = new ThrowingDatabase(new Exception());
        var settings = new ServerHostOptions { SqlServer = "localhost", Database = "GoDental_Development",
            ServiceSid = "SYNTHETIC_WRONG_SID", HttpsCertificateThumbprint = new string('A', 40) };
        var check = new ServerDeploymentCheck(Options.Create(settings), new SyntheticEnvironment("Production"), new NeverTls(), database);
        Assert.Equal(OperatingSystem.IsWindows() ? ServerDeploymentStatus.ServiceIdentityDenied : ServerDeploymentStatus.WindowsRequired,
            await check.CheckAsync());
        Assert.Equal(0, database.Calls);
    }

    [Theory]
    [InlineData("localhost", true, true, 0, true)]
    [InlineData("other.example", true, true, 0, false)]
    [InlineData("localhost", false, true, 0, false)]
    [InlineData("localhost", true, false, 0, false)]
    [InlineData("localhost", true, true, -1, false)]
    [InlineData("localhost", true, true, 1, false)]
    public void CertificateRequiresExactSanServerEkuPrivateKeyAndValidity(string host, bool serverEku, bool privateKey, int dateCase, bool accepted)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=deliberately-not-the-server", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName(host);
        request.CertificateExtensions.Add(names.Build());
        if (serverEku) request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        var now = DateTimeOffset.UtcNow;
        using var certificate = request.CreateSelfSigned(dateCase == 1 ? now.AddDays(1) : now.AddDays(-3),
            dateCase == -1 ? now.AddDays(-1) : now.AddDays(3));
        using var publicOnly = new X509Certificate2(certificate.RawData);
        Assert.Equal(accepted, ServerDeploymentPolicy.ValidCertificate(privateKey ? certificate : publicOnly, "localhost", now));
    }

    [Fact]
    public void CertificateCannotUseCommonNameWithoutSan()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.False(ServerDeploymentPolicy.ValidCertificate(certificate, "localhost", DateTimeOffset.UtcNow));
    }

    private static ServerHostOptions Settings(string url = "https://localhost:7443") => new()
    { Url = url, SqlServer = "localhost", Database = "GoDental_Development", LocalDevelopmentSqlCertificate = true };

    private sealed class NeverTls : IServerTlsCertificate
    {
        public X509Certificate2 GetCertificate() => throw new InvalidOperationException("No debe seleccionar un certificado implícito.");
    }
    private sealed class ThrowingDatabase(Exception failure) : IDbConnectionFactory
    {
        public int Calls;
        public Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<DbConnection>(failure);
        }
    }
    private sealed class SyntheticEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "SyntheticDeploymentTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
