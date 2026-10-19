using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;

public sealed class InventoryService(IInventoryRepository repository, IUserSession session)
{
    public bool CanRead => session.IsAuthenticated && session.Permissions.Contains("INVENTARIO.VER");
    public bool CanAdjust => CanRead && session.Permissions.Contains("INVENTARIO.AJUSTAR");
    public bool CanSeeCosts => CanRead && session.Permissions.Contains("INVENTARIO.COSTOS_VER");
    private int UserId => checked((int)session.UserId!.Value);
    public Task<IReadOnlyList<CatalogOption>> WarehousesAsync(CancellationToken ct = default)
    {
        if (!CanRead) throw new UnauthorizedAccessException();
        return repository.WarehousesAsync(UserId, ct);
    }
    public Task<InventorySnapshot> ReadAsync(int productId, int warehouseId, CancellationToken ct = default)
    {
        if (!CanRead) throw new UnauthorizedAccessException();
        if (productId <= 0 || warehouseId <= 0) throw new ArgumentException("Selecciona producto y almacén.");
        return repository.ReadAsync(productId, warehouseId, UserId, ct);
    }
    public Task<MovementPage> MovementsAsync(MovementFilter filter, CancellationToken ct = default)
    {
        if (!CanRead) throw new UnauthorizedAccessException();
        if (filter.ProductId <= 0 || filter.Page < 1 || filter.PageSize is < 1 or > 100 || filter.Page > int.MaxValue / filter.PageSize
            || filter.From > filter.Until || filter.Until >= DateTime.MaxValue.Date)
            throw new ArgumentException("Revisa las fechas y la página del Kardex.");
        return repository.MovementsAsync(filter, UserId, ct);
    }
    public Task<Result<long>> AdjustAsync(InventoryAdjustment request, CancellationToken ct = default)
    {
        if (!CanAdjust) return Task.FromResult(Result<long>.Failure("FORBIDDEN", "No tienes permiso para ajustar inventario."));
        var error = InventoryValidator.Validate(request);
        if (error is not null) return Task.FromResult(Result<long>.Failure("VALIDATION", error));
        return repository.AdjustAsync(request with { Reason = request.Reason.Trim(), NewLotNumber = request.NewLotNumber?.Trim() }, UserId, (int?)session.WorkstationId, ct);
    }
    public Task<Result<long>> SetLotStateAsync(LotStateChange request, CancellationToken ct = default)
    {
        if (!CanAdjust) return Task.FromResult(Result<long>.Failure("FORBIDDEN", "No tienes permiso para bloquear o reactivar lotes."));
        if (request.LotId <= 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return Task.FromResult(Result<long>.Failure("VALIDATION", "Selecciona un lote y escribe el motivo (hasta 500 caracteres)."));
        return repository.SetLotStateAsync(request with { Reason = request.Reason.Trim() }, UserId, (int?)session.WorkstationId, ct);
    }
}
