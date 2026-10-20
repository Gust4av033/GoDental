namespace GoDental.Application.DTOs;
public sealed class UserSummary
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Roles { get; set; } = "";
    public bool Active { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastAccess { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
