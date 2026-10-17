using System.Collections.Frozen;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;

namespace GoDental.Application.Services;

public sealed class UserSession : ISessionController
{
    public bool IsAuthenticated { get; private set; }
    public string? ConnectionScope { get; private set; }
    public long Revision { get; private set; }
    public long? UserId { get; private set; }
    public string? UserName { get; private set; }
    public string? DisplayName { get; private set; }
    public long? WorkstationId { get; private set; }
    public IReadOnlySet<string> Roles { get; private set; } = Array.Empty<string>().ToFrozenSet();
    public IReadOnlySet<string> Permissions { get; private set; } = Array.Empty<string>().ToFrozenSet();
    public event EventHandler? Changed;

    public void Begin(UserIdentity identity)
    {
        UserId = identity.Id; UserName = identity.UserName; DisplayName = identity.DisplayName;
        ConnectionScope = identity.ConnectionScope; Revision++;
        WorkstationId = identity.WorkstationId;
        Roles = identity.Roles.ToFrozenSet(StringComparer.Ordinal);
        Permissions = identity.MustChangePassword ? Array.Empty<string>().ToFrozenSet() : identity.Permissions.ToFrozenSet(StringComparer.Ordinal);
        IsAuthenticated = !identity.MustChangePassword;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        IsAuthenticated = false; UserId = null; UserName = null; DisplayName = null; WorkstationId = null;
        ConnectionScope = null; Revision++;
        Roles = Array.Empty<string>().ToFrozenSet(); Permissions = Array.Empty<string>().ToFrozenSet();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
