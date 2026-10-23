namespace GoDental.Application.DTOs;

public sealed class ServerLoginRequest
{
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Workstation { get; init; } = "";
    public override string ToString() => "Credenciales de servidor omitidas";
}

public sealed class ServerAccessGrant(string accessToken, DateTimeOffset expiresAt)
{
    public string AccessToken { get; } = accessToken;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => "Sesión de servidor omitida";
}

public sealed class ServerCredential
{
    public byte[] RowVersion { get; init; } = [];
    public string PasswordHash { get; init; } = "";
    public override string ToString() => "Verificador de credencial omitido";
}

public enum ServerLoginOutcome { Denied, Granted, PasswordChangeRequired }

public sealed class ServerLoginDecision
{
    public ServerLoginOutcome Outcome { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed record ServerOperator(int UserId, string UserName, string DisplayName,
    int WorkstationId, DateTimeOffset ExpiresAt, IReadOnlyList<string> Permissions);
