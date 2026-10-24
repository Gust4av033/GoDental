using System.Security.Claims;
using System.Text.Encodings.Web;
using GoDental.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GoDental.Server.Security;

public sealed class OpaqueSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IServerAuthenticationService sessions,
    ServerRequestContext context) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GoDentalSession";

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return base.HandleChallengeAsync(properties);
    }

    internal static string? Token(HttpRequest request)
    {
        if (request.Headers.Authorization.Count != 1) return null;
        var header = request.Headers.Authorization.ToString();
        if (header.Length != 71 || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..];
        return token.All(char.IsAsciiHexDigit) ? token : null;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Token(Request) is not { } token) return AuthenticateResult.NoResult();
        var identity = await sessions.AuthenticateAsync(token, Context.RequestAborted);
        if (identity is null) return AuthenticateResult.Fail("Sesión inválida.");
        context.Set(identity);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, identity.UserName)
        };
        claims.AddRange(identity.Permissions.Select(permission => new Claim("permission", permission)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
