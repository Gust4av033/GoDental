using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default);
    Task<ProductCatalogs> GetCatalogsAsync(CancellationToken cancellationToken = default);
    Task<Result<int>> SaveAsync(ProductData product, int userId, int? workstationId, CancellationToken cancellationToken = default);
    Task<Result<int>> CreateCategoryAsync(string name, int userId, int? workstationId, CancellationToken cancellationToken = default);
}
