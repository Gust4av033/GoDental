using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GoDental.Application.DTOs;
using GoDental.Infrastructure.Data;

namespace GoDental.Server.Tests;

public sealed class ServerHttpTests
{
    [Fact]
    public async Task ParallelOperatorsKeepSeparateRequestContexts()
    {
        await using var app = new ServerFixture(); using var first = app.Client(); using var second = app.Client();
        var firstGrant = await ServerFixture.SignIn(first);
        using var secondLogin = await second.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "second.operator", Password = ServerFixture.SyntheticPassword, Workstation = "TEST-PC" });
        secondLogin.EnsureSuccessStatusCode();
        var secondGrant = (await secondLogin.Content.ReadFromJsonAsync<ServerAccessGrant>())!;
        first.DefaultRequestHeaders.Authorization = new("Bearer", firstGrant.AccessToken);
        second.DefaultRequestHeaders.Authorization = new("Bearer", secondGrant.AccessToken);
        for (var round = 0; round < 3; round++)
        {
            var profiles = await Task.WhenAll(first.GetFromJsonAsync<ServerOperator>("/api/session"),
                second.GetFromJsonAsync<ServerOperator>("/api/session"));
            Assert.Equal(11, profiles[0]!.UserId); Assert.Equal(33, profiles[1]!.UserId);
        }
    }

    [Fact]
    public async Task LivenessDoesNotRequireSqlOrClaimFiscalReadiness()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"status\":\"RUNNING\"}", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, app.Repository.ReadCalls);
    }

    [Theory]
    [InlineData("GET", "/api/session")]
    [InlineData("DELETE", "/api/session")]
    [InlineData("GET", "/api/fiscal/readiness")]
    public async Task ProtectedRoutesRejectAnonymousAndCallerSuppliedIdentity(string method, string path)
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        using var request = new HttpRequestMessage(new(method), path);
        request.Headers.Add("X-User-Id", "9999"); request.Headers.Add("X-Permissions", "FISCAL.VER");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains(response.Headers.WwwAuthenticate, x => x.Scheme == "Bearer");
    }

    [Fact]
    public async Task HttpAndForwardedProtoCannotReachCredentialVerification()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        client.BaseAddress = new("http://localhost"); client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "operator", Password = ServerFixture.SyntheticPassword, Workstation = "TEST-PC" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location); Assert.Equal(0, app.Repository.ReadCalls);
    }

    [Fact]
    public async Task ProfileComesFromAuthenticatedSessionAndTokensAreOpaqueDistinct()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        var first = await ServerFixture.SignIn(client); var second = await ServerFixture.SignIn(client);
        Assert.Equal(64, first.AccessToken.Length); Assert.NotEqual(first.AccessToken, second.AccessToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        client.DefaultRequestHeaders.Add("X-User-Id", "9999"); client.DefaultRequestHeaders.Add("X-Workstation-Id", "9999");
        using var response = await client.GetAsync("/api/session"); response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<ServerOperator>();
        Assert.Equal(11, profile!.UserId); Assert.Equal(22, profile.WorkstationId);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PermissionsAreRevalidatedWithoutMintingAnotherToken()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        var grant = await ServerFixture.SignIn(client); client.DefaultRequestHeaders.Authorization = new("Bearer", grant.AccessToken);
        using var allowed = await client.GetAsync("/api/fiscal/readiness"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains("\"transmissionEnabled\":false", await allowed.Content.ReadAsStringAsync());
        app.Repository.Permissions = ["VENTAS.VER"];
        using var denied = await client.GetAsync("/api/fiscal/readiness"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var profile = await client.GetFromJsonAsync<ServerOperator>("/api/session");
        Assert.DoesNotContain("FISCAL.VER", profile!.Permissions);
    }

    [Fact]
    public async Task LogoutRevokesOnlyPresentedSession()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        var first = await ServerFixture.SignIn(client); var second = await ServerFixture.SignIn(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        using var logout = await client.DeleteAsync("/api/session"); Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var denied = await client.GetAsync("/api/session"); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", second.AccessToken);
        using var allowed = await client.GetAsync("/api/session"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentialsAndUnknownUsersHaveSameResponse()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        async Task<string> Attempt(string user)
        {
            using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
                { UserName = user, Password = "synthetic-wrong-password", Workstation = "TEST-PC" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }
        Assert.Equal(await Attempt("operator"), await Attempt("missing-user"));
        Assert.Equal(0, app.Repository.SessionCount);
    }

    [Fact]
    public async Task MandatoryPasswordChangeCannotIssueASession()
    {
        await using var app = new ServerFixture(); using var client = app.Client(); app.Repository.MustChange = true;
        using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "operator", Password = ServerFixture.SyntheticPassword, Workstation = "TEST-PC" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("PASSWORD_CHANGE_REQUIRED", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, app.Repository.SessionCount);
    }

    [Theory]
    [InlineData("\"userId\":9999")]
    [InlineData("\"permissions\":[\"FISCAL.VER\"]")]
    public async Task UnexpectedJsonIdentityFieldsFailBeforeAuthentication(string field)
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        using var content = new StringContent("{\"userName\":\"operator\",\"password\":\"synthetic\",\"workstation\":\"TEST-PC\"," + field + "}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/session", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(0, app.Repository.ReadCalls);
    }

    [Fact]
    public async Task QueryAndCookieTokensCannotAuthenticateAndDoNotLeakToLogs()
    {
        await using var app = new ServerFixture(); using var client = app.Client(); var grant = await ServerFixture.SignIn(client);
        client.DefaultRequestHeaders.Add("Cookie", "access_token=" + grant.AccessToken);
        using var response = await client.GetAsync("/api/session?access_token=" + grant.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(grant.AccessToken, string.Join('\n', app.Logs.Messages));
        Assert.DoesNotContain(ServerFixture.SyntheticPassword, string.Join('\n', app.Logs.Messages));
    }

    [Theory]
    [InlineData("Bearer abc")]
    [InlineData("Basic synthetic")]
    [InlineData("Bearer ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public async Task MalformedBearerDoesNotQuerySql(string header)
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", header);
        using var response = await client.GetAsync("/api/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); Assert.Equal(0, app.Repository.ValidationCalls);
    }

    [Fact]
    public async Task OversizedBodyIsRejectedWithoutCredentialIo()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        using var content = new StringContent(new string('x', 9000), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/session", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode); Assert.Equal(0, app.Repository.ReadCalls);
    }

    [Fact]
    public async Task LoginRateLimitDoesNotProcessEleventhAttempt()
    {
        await using var app = new ServerFixture(); using var client = app.Client();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
                { UserName = "missing-user", Password = "synthetic-wrong-password", Workstation = "TEST-PC" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using var last = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "missing-user", Password = "synthetic-wrong-password", Workstation = "TEST-PC" });
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode); Assert.Equal(10, app.Repository.ReadCalls);
    }

    [Fact]
    public async Task ProviderFailureIsSanitizedInResponseAndLogs()
    {
        await using var app = new ServerFixture(); using var client = app.Client(); app.Repository.ThrowOnRead = true;
        using var response = await client.PostAsJsonAsync("/api/session", new ServerLoginRequest
            { UserName = "operator", Password = ServerFixture.SyntheticPassword, Workstation = "TEST-PC" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("SYNTHETIC_PRIVATE_PROVIDER_DETAIL", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("SYNTHETIC_PRIVATE_PROVIDER_DETAIL", string.Join('\n', app.Logs.Messages));
        Assert.DoesNotContain(ServerFixture.SyntheticPassword, string.Join('\n', app.Logs.Messages));
    }

    [Fact]
    public void SensitiveDtoStringRepresentationsDoNotRevealValues()
    {
        Assert.DoesNotContain("SYNTHETIC_SECRET", new ServerLoginRequest { Password = "SYNTHETIC_SECRET" }.ToString());
        Assert.DoesNotContain("SYNTHETIC_SECRET", new ServerAccessGrant("SYNTHETIC_SECRET", DateTimeOffset.UtcNow).ToString());
        Assert.DoesNotContain("SYNTHETIC_SECRET", new ServerCredential { PasswordHash = "SYNTHETIC_SECRET" }.ToString());
    }

    [Fact]
    public void ServerCannotTrustLocalSqlCertificateOutsideLocalDevelopment()
    {
        Assert.Throws<ArgumentException>(() => new ServerSqlConnectionFactory(new("localhost", "GoDental_Development", LocalDevelopment: true), false));
        Assert.Throws<ArgumentException>(() => new ServerSqlConnectionFactory(new("remote-host", "GoDental_Development", LocalDevelopment: true), true));
        Assert.Throws<ArgumentException>(() => new ServerSqlConnectionFactory(new("localhost", "GoDental", LocalDevelopment: true), true));
    }
}
