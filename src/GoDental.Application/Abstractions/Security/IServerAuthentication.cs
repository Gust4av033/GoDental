using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Security;

public interface IServerAuthenticationService
{
    Task<Result<ServerAccessGrant>> SignInAsync(ServerLoginRequest request, CancellationToken ct = default);
    Task<ServerOperator?> AuthenticateAsync(string token, CancellationToken ct = default);
    Task SignOutAsync(string token, CancellationToken ct = default);
}

public interface IServerAuthenticationRepository
{
    Task<ServerCredential?> ReadCredentialAsync(string userName, CancellationToken ct);
    Task<ServerLoginDecision> CompleteSignInAsync(string userName, byte[]? expectedVersion,
        bool passwordVerified, string workstation, byte[] tokenHash, CancellationToken ct);
    Task<ServerOperator?> ValidateSessionAsync(byte[] tokenHash, CancellationToken ct);
    Task RevokeSessionAsync(byte[] tokenHash, CancellationToken ct);
}
