using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface IPendingOperationStore
{
    bool IsDurable { get; }
    Task<IReadOnlyList<PendingOperation>> LoadAsync(string scope, long userId, long? workstationId, CancellationToken ct = default);
    // Atomically refuse another pending operation for this identity before adding this one.
    Task<bool> TryAddAsync(PendingOperation pending, CancellationToken ct = default);
    Task RemoveAsync(PendingOperation pending, CancellationToken ct = default);
}
