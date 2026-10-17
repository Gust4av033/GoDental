using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IUserRepository
{
    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogOption>> RolesAsync(CancellationToken ct = default);
    Task<Result> CreateAsync(string userName, string displayName, int roleId, string hash, int actor, int? equipment, CancellationToken ct = default);
    Task<Result> SetActiveAsync(UserSummary user, bool active, string reason, int actor, int? equipment, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(UserSummary user, string hash, string reason, int actor, int? equipment, CancellationToken ct = default);
}
