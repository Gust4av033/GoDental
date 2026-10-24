using System.Collections.Concurrent;
using System.Net.Http.Json;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GoDental.Server.Tests;

// Sólo identidad sintética de pruebas HTTP. La suite DatabaseTests usa los procedimientos reales.
internal sealed class ServerFixture : WebApplicationFactory<Program>
{
    public const string SyntheticPassword = "synthetic-only-server-test-phrase";
    public FakeIdentityRepository Repository { get; } = new();
    public CapturedLogs Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Server:SqlServer"] = "localhost", ["Server:Database"] = "GoDental_Development"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IServerAuthenticationRepository>();
            services.AddSingleton<IServerAuthenticationRepository>(Repository);
        });
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    public HttpClient Client() => CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });

    public static async Task<ServerAccessGrant> SignIn(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "operator", Password = SyntheticPassword, Workstation = "TEST-PC" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServerAccessGrant>())!;
    }
}

internal sealed class FakeIdentityRepository : IServerAuthenticationRepository
{
    private readonly string _hash = new Pbkdf2PasswordHasher().Hash(ServerFixture.SyntheticPassword);
    private readonly ConcurrentDictionary<string, ServerOperator> _sessions = new();
    public string[] Permissions { get; set; } = ["FISCAL.VER", "VENTAS.VER"];
    public bool MustChange { get; set; }
    public bool ThrowOnRead { get; set; }
    public int ReadCalls;
    public int ValidationCalls;
    public int SessionCount => _sessions.Count;

    public Task<ServerCredential?> ReadCredentialAsync(string userName, CancellationToken ct)
    {
        Interlocked.Increment(ref ReadCalls);
        if (ThrowOnRead) throw new InvalidOperationException("SYNTHETIC_PRIVATE_PROVIDER_DETAIL");
        return Task.FromResult(userName is "operator" or "second.operator" ? new ServerCredential { PasswordHash = _hash, RowVersion = new byte[8] } : null);
    }

    public Task<ServerLoginDecision> CompleteSignInAsync(string userName, byte[]? expectedVersion,
        bool passwordVerified, string workstation, byte[] tokenHash, CancellationToken ct)
    {
        var outcome = passwordVerified && workstation == "TEST-PC"
            ? MustChange ? ServerLoginOutcome.PasswordChangeRequired : ServerLoginOutcome.Granted
            : ServerLoginOutcome.Denied;
        if (outcome == ServerLoginOutcome.Granted)
            _sessions[Convert.ToHexString(tokenHash)] = new(userName == "operator" ? 11 : 33,
                userName, "Operador sintético", 22, DateTimeOffset.UtcNow.AddHours(8), []);
        return Task.FromResult(new ServerLoginDecision { Outcome = outcome, ExpiresAt = DateTimeOffset.UtcNow.AddHours(8) });
    }

    public Task<ServerOperator?> ValidateSessionAsync(byte[] tokenHash, CancellationToken ct)
    {
        Interlocked.Increment(ref ValidationCalls);
        return Task.FromResult(_sessions.TryGetValue(Convert.ToHexString(tokenHash), out var profile)
            ? profile with { Permissions = Array.AsReadOnly((string[])Permissions.Clone()) } : null);
    }

    public Task RevokeSessionAsync(byte[] tokenHash, CancellationToken ct)
    {
        _sessions.TryRemove(Convert.ToHexString(tokenHash), out _);
        return Task.CompletedTask;
    }
}

internal sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Capture(Messages);
    public void Dispose() { }
    private sealed class Capture(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState,Exception?,string> formatter) => messages.Enqueue(formatter(state,exception) + exception?.ToString());
    }
}
