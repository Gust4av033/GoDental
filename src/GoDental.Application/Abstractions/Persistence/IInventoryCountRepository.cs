using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface IInventoryCountRepository
{
    Task<InventoryCountPage> SearchAsync(InventoryCountFilter filter, int userId, CancellationToken ct = default);
    Task<InventoryCountSnapshot?> ReadAsync(long countId, int userId, CancellationToken ct = default);
    Task<Result<long>> StartAsync(StartInventoryCount request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> CaptureAsync(CaptureInventoryCount request, int userId, int? workstationId, CancellationToken ct = default);
    Task<Result<long>> FinalizeAsync(FinalizeInventoryCount request, bool cancel, int userId, int? workstationId, CancellationToken ct = default);
}
