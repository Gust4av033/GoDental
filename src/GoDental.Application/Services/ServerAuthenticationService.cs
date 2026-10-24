using System.Security.Cryptography;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Services;

public sealed class ServerAuthenticationService(IServerAuthenticationRepository repository,
    IPasswordHasher hasher) : IServerAuthenticationService
{
    private const string InvalidLogin = "Usuario o contraseña incorrectos, o acceso temporalmente bloqueado.";
    // Mismo coste que el verificador PBKDF2 de la aplicación; evita una salida rápida si no existe usuario.
    private const string DummyHash = "PBKDF2-SHA256$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public async Task<Result<ServerAccessGrant>> SignInAsync(ServerLoginRequest request, CancellationToken ct = default)
    {
        if (request.UserName is null || request.Password is null || request.Workstation is null ||
            request.UserName.Trim().Length is < 1 or > 60 || request.Password.Length is < 1 or > 128 ||
            request.Workstation.Trim().Length is < 1 or > 120)
            return Result<ServerAccessGrant>.Failure("INVALID_CREDENTIALS", InvalidLogin);

        var userName = request.UserName.Trim();
        var credential = await repository.ReadCredentialAsync(userName, ct);
        var verified = await Task.Run(() => hasher.Verify(request.Password, credential?.PasswordHash ?? DummyHash), ct);
        var raw = RandomNumberGenerator.GetBytes(32);
        try
        {
            var decision = await repository.CompleteSignInAsync(userName, credential?.RowVersion,
                verified && credential is not null, request.Workstation.Trim(), SHA256.HashData(raw), ct);
            return decision.Outcome switch
            {
                ServerLoginOutcome.Granted => Result<ServerAccessGrant>.Success(new(Convert.ToHexString(raw), decision.ExpiresAt)),
                ServerLoginOutcome.PasswordChangeRequired => Result<ServerAccessGrant>.Failure(
                    "PASSWORD_CHANGE_REQUIRED", "Cambia tu contraseña desde GoDental antes de acceder al servidor."),
                _ => Result<ServerAccessGrant>.Failure("INVALID_CREDENTIALS", InvalidLogin)
            };
        }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    public Task<ServerOperator?> AuthenticateAsync(string token, CancellationToken ct = default) =>
        TokenHash(token) is { } hash ? repository.ValidateSessionAsync(hash, ct) : Task.FromResult<ServerOperator?>(null);

    public Task SignOutAsync(string token, CancellationToken ct = default) =>
        TokenHash(token) is { } hash ? repository.RevokeSessionAsync(hash, ct) : Task.CompletedTask;

    private static byte[]? TokenHash(string token)
    {
        if (token is null || token.Length != 64 || token.Any(c => !char.IsAsciiHexDigit(c))) return null;
        var raw = Convert.FromHexString(token);
        try { return SHA256.HashData(raw); }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
}
