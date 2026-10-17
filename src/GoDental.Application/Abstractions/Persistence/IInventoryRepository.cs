using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface IInventoryRepository
{
    Task<IReadOnlyList<CatalogOption>> WarehousesAsync(int userId, CancellationToken ct = default);
    Task<InventorySnapshot> ReadAsync(int productId, int warehouseId, int userId, CancellationToken ct = default);
    Task<MovementPage> MovementsAsync(MovementFilter filter, int userId, CancellationToken ct = default);
    Task<Result<long>> AdjustAsync(InventoryAdjustment request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> SetLotStateAsync(LotStateChange request, int userId, int? workstationId, CancellationToken ct = default);
}
