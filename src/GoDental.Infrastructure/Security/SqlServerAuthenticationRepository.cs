using System.Data;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Security;

public sealed class SqlServerAuthenticationRepository(IDbConnectionFactory factory) : IServerAuthenticationRepository
{
    public async Task<ServerCredential?> ReadCredentialAsync(string userName, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return await c.QuerySingleOrDefaultAsync<ServerCredential>(Command("seg.ServidorLoginLeer", new { userName }, ct));
    }

    public async Task<ServerLoginDecision> CompleteSignInAsync(string userName, byte[]? expectedVersion,
        bool passwordVerified, string workstation, byte[] tokenHash, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        return await c.QuerySingleAsync<ServerLoginDecision>(Command("seg.ServidorLoginFinalizar",
            new { userName, expectedVersion, passwordVerified, workstation, tokenHash }, ct));
    }

    public async Task<ServerOperator?> ValidateSessionAsync(byte[] tokenHash, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        using var rows = await c.QueryMultipleAsync(Command("seg.ServidorSesionValidar", new { tokenHash }, ct));
        var profile = await rows.ReadSingleOrDefaultAsync<ProfileRow>();
        if (profile is null) return null;
        var permissions = (await rows.ReadAsync<string>()).ToArray();
        return new(profile.UserId, profile.UserName, profile.DisplayName, profile.WorkstationId,
            profile.ExpiresAt, Array.AsReadOnly(permissions));
    }

    public async Task RevokeSessionAsync(byte[] tokenHash, CancellationToken ct)
    {
        await using var c = await factory.OpenConnectionAsync(ct);
        await c.ExecuteAsync(Command("seg.ServidorSesionRevocar", new { tokenHash }, ct));
    }

    private static CommandDefinition Command(string name, object args, CancellationToken ct) =>
        new(name, args, commandType: CommandType.StoredProcedure, commandTimeout: 15, cancellationToken: ct);

    private sealed class ProfileRow
    {
        public int UserId { get; init; }
        public string UserName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int WorkstationId { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}
