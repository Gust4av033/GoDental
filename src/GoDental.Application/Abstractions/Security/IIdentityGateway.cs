using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Security;

public interface IIdentityGateway
{
    Task<bool> NeedsAdministratorAsync(CancellationToken cancellationToken = default);
    Task<Result<UserIdentity>> CreateAdministratorAsync(string userName, string displayName, string password, string workstation, CancellationToken cancellationToken = default);
    Task<Result<UserIdentity>> SignInAsync(string userName, string password, string workstation, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task SignOutAsync(int userId, int? workstationId, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface ISessionController : IUserSession
{
    string? ConnectionScope { get; }
    long Revision { get; }
    void Begin(UserIdentity identity);
    void Clear();
    event EventHandler? Changed;
}
