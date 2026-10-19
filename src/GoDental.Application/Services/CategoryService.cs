using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Services;
public sealed class CategoryService(ICategoryRepository repository, IUserSession session)
{
    public bool CanManage => session.IsAuthenticated && session.Permissions.Contains(ProductService.ReadPermission) && session.Permissions.Contains(ProductService.ManagePermission);
    public Task<IReadOnlyList<CategoryData>> ListAsync(CancellationToken ct = default)
    {
        if (!CanManage) throw new UnauthorizedAccessException();
        return repository.ListAsync(checked((int)session.UserId!.Value), ct);
    }
    public Task<Result<int>> SaveAsync(CategoryChange change, CancellationToken ct = default)
    {
        if (!CanManage) return Task.FromResult(Result<int>.Failure("FORBIDDEN", "No tienes permiso para gestionar categorías."));
        if (change.Id <= 0 || string.IsNullOrWhiteSpace(change.Name) || change.Name.Trim().Length is < 2 or > 120)
            return Task.FromResult(Result<int>.Failure("VALIDATION", "Selecciona una categoría y escribe un nombre de 2 a 120 caracteres."));
        return repository.SaveAsync(change with { Name = change.Name.Trim() }, checked((int)session.UserId!.Value), (int?)session.WorkstationId, ct);
    }
}
