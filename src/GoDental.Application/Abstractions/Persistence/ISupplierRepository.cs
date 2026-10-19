using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface ISupplierRepository
{
    Task<SupplierPage> SearchAsync(SupplierSearch search, int userId, CancellationToken ct = default);
    Task<SupplierSnapshot?> ReadAsync(int supplierId, int userId, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogOption>> CurrenciesAsync(int userId, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(SupplierChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<int>> SaveContactAsync(SupplierContactChange request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<int>> SaveProductAsync(SupplierProductChange request, int userId, int? workstationId, CancellationToken ct = default);
}
