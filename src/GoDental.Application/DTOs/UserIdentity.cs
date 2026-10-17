namespace GoDental.Application.DTOs;

public sealed record UserIdentity(int Id, string UserName, string DisplayName, int WorkstationId,
    string[] Roles, string[] Permissions, bool MustChangePassword, string ConnectionScope = "");
