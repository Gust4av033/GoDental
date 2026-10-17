using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryData>> ListAsync(int userId, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(CategoryChange change, int userId, int? workstationId, CancellationToken ct = default);
}
