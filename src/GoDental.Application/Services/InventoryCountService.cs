using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;

namespace GoDental.Application.Services;

public sealed class InventoryCountService(IInventoryCountRepository repository, IUserSession session)
{
    public bool CanRead => session.IsAuthenticated && session.Permissions.Contains("INVENTARIO.VER")
        && session.Permissions.Contains("INVENTARIO.CONTEO");
    public bool CanClose => CanRead && session.Permissions.Contains("INVENTARIO.AJUSTAR");
    private int UserId => checked((int)session.UserId!.Value);
    private int? WorkstationId => checked((int?)session.WorkstationId);

    public Task<InventoryCountPage> SearchAsync(InventoryCountFilter filter, CancellationToken ct = default)
    {
        if (!CanRead) throw new UnauthorizedAccessException("No tienes permiso para consultar conteos.");
        if (filter.WarehouseId is <= 0 || filter.State is not (null or "ABIERTO" or "CERRADO" or "CANCELADO")
            || filter.Page < 1 || filter.PageSize is < 1 or > 100 || filter.Page > int.MaxValue / filter.PageSize)
            throw new ArgumentException("Revisa almacén, estado y página del listado.", nameof(filter));
        return repository.SearchAsync(filter, UserId, ct);
    }

    public Task<InventoryCountSnapshot?> ReadAsync(long countId, CancellationToken ct = default)
    {
        if (!CanRead) throw new UnauthorizedAccessException("No tienes permiso para consultar conteos.");
        if (countId <= 0) throw new ArgumentOutOfRangeException(nameof(countId));
        return repository.ReadAsync(countId, UserId, ct);
    }

    public Task<Result<long>> StartAsync(StartInventoryCount request, CancellationToken ct = default)
    {
        if (!CanRead) return Denied();
        if (InventoryCountValidator.Validate(request) is { } error) return Invalid(error);
        return repository.StartAsync(request with { Reason = request.Reason.Trim(), ProductIds = request.ProductIds.ToArray() }, UserId, WorkstationId, ct);
    }

    public Task<Result<long>> CaptureAsync(CaptureInventoryCount request, CancellationToken ct = default)
    {
        if (!CanRead) return Denied();
        if (InventoryCountValidator.Validate(request) is { } error) return Invalid(error);
        return repository.CaptureAsync(request, UserId, WorkstationId, ct);
    }

    public Task<Result<long>> CloseAsync(FinalizeInventoryCount request, CancellationToken ct = default) => FinalizeAsync(request, false, ct);
    public Task<Result<long>> CancelAsync(FinalizeInventoryCount request, CancellationToken ct = default) => FinalizeAsync(request, true, ct);

    private Task<Result<long>> FinalizeAsync(FinalizeInventoryCount request, bool cancel, CancellationToken ct)
    {
        if (!CanRead || (!cancel && !CanClose)) return Denied();
        if (InventoryCountValidator.Validate(request) is { } error) return Invalid(error);
        return repository.FinalizeAsync(request with { Reason = request.Reason.Trim() }, cancel, UserId, WorkstationId, ct);
    }

    private static Task<Result<long>> Denied() => Task.FromResult(Result<long>.Failure("FORBIDDEN", "No tienes permiso para esta operación de conteo."));
    private static Task<Result<long>> Invalid(string message) => Task.FromResult(Result<long>.Failure("VALIDATION", message));
}
