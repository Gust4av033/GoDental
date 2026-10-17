using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Application.Services;

public sealed class ProductService(IProductRepository repository, IUserSession session)
{
    public const string ReadPermission = "INVENTARIO.VER";
    public const string ManagePermission = "INVENTARIO.PRODUCTOS_GESTIONAR";
    public bool CanRead => session.IsAuthenticated && session.Permissions.Contains(ReadPermission);
    public bool CanManage => CanRead && session.Permissions.Contains(ManagePermission);

    public async Task<Result<ProductPage>> SearchAsync(ProductSearch search, CancellationToken ct = default)
    {
        if (!CanRead) return Result<ProductPage>.Failure("FORBIDDEN", "Inicia sesión con permiso para consultar inventario.");
        if (search.Search.Length > 200 || search.Page < 1 || search.PageSize is < 1 or > 100 || search.Page > int.MaxValue / search.PageSize)
            return Result<ProductPage>.Failure("VALIDATION", "Revisa los filtros de búsqueda.");
        return Result<ProductPage>.Success(await repository.SearchAsync(search, ct));
    }
    public async Task<Result<ProductCatalogs>> GetCatalogsAsync(CancellationToken ct = default) => !CanRead
        ? Result<ProductCatalogs>.Failure("FORBIDDEN", "No tienes permiso para consultar inventario.")
        : Result<ProductCatalogs>.Success(await repository.GetCatalogsAsync(ct));

    public Task<Result<int>> SaveAsync(ProductData product, CancellationToken ct = default)
    {
        if (!CanManage) return Task.FromResult(Result<int>.Failure("FORBIDDEN", "No tienes permiso para guardar productos."));
        var error = ProductValidator.Validate(product);
        if (error is not null) return Task.FromResult(Result<int>.Failure("VALIDATION", error));
        return repository.SaveAsync(product, checked((int)session.UserId!.Value), (int?)session.WorkstationId, ct);
    }
    public Task<Result<int>> CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        if (!CanManage) return Task.FromResult(Result<int>.Failure("FORBIDDEN", "No tienes permiso para crear categorías."));
        name = name.Trim();
        if (name.Length is < 2 or > 120) return Task.FromResult(Result<int>.Failure("VALIDATION", "La categoría debe tener de 2 a 120 caracteres."));
        return repository.CreateCategoryAsync(name, checked((int)session.UserId!.Value), (int?)session.WorkstationId, ct);
    }
}
