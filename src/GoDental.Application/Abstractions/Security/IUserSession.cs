namespace GoDental.Application.Abstractions.Security;

public interface IUserSession
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? UserName { get; }
    string? DisplayName { get; }
    long? WorkstationId { get; }
    IReadOnlySet<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
}
