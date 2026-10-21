using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Services;

public sealed class MvpOperationService(IMvpOperationRepository repository, IUserSession session, IPendingOperationStore? pendingStore = null) : MvpServiceAccess(session)
{
    private readonly IPendingOperationStore store = pendingStore ?? new VolatileOperationStore();
    public bool IsDurable => store.IsDurable;
    public event EventHandler? PendingChanged;
    public event EventHandler<RecoveredOperation>? Recovered;

    private string Scope => CurrentSession is ISessionController { ConnectionScope.Length: > 0 } bound ? bound.ConnectionScope :
        !store.IsDurable ? "volatile-tests" : throw new InvalidOperationException("La sesión no tiene una base vinculada.");

    public Task<MvpReceipt?> FindAsync(Guid id, CancellationToken ct = default)
    {
        Read();
        if (id == Guid.Empty) throw new ArgumentException("Identificador vacío.");
        return repository.FindOperationAsync(id, UserId, ct);
    }

    public Task<IReadOnlyList<PendingOperation>> PendingAsync(CancellationToken ct = default)
    {
        Read();
        return store.LoadAsync(Scope, UserId, WorkstationId, ct);
    }

    public async Task<PendingOperation> RememberAsync(object request, CancellationToken ct = default)
    {
        Read();
        var pending = new PendingOperation(Scope, UserId, WorkstationId, OperationIdentity.Describe(request), DateTimeOffset.UtcNow);
        if (!await store.TryAddAsync(pending, ct)) throw new InvalidOperationException("Hay una solicitud pendiente en este equipo. Abre Operaciones pendientes y resuélvela antes de registrar otra.");
        PendingChanged?.Invoke(this, EventArgs.Empty);
        return pending;
    }

    private void Own(PendingOperation pending)
    {
        Read();
        if (pending.Scope != Scope || pending.UserId != UserId || pending.WorkstationId != WorkstationId)
            throw new UnauthorizedAccessException("El pendiente pertenece a otra base, cuenta o estación.");
    }

    public async Task<OperationResolution> ResolveAsync(PendingOperation pending, bool rejectIfAbsent = false, CancellationToken ct = default)
    {
        Own(pending);
        var result = await repository.ResolveOperationAsync(pending.Ticket, rejectIfAbsent, UserId, WorkstationId, ct);
        // Recheck after IO: another user must not consume a previous session's result.
        Own(pending);
        if (result.State is OperationResolutionState.Confirmed or OperationResolutionState.RejectedDefinitively)
            await AcknowledgeAsync(pending, result, ct);
        return result;
    }

    public async Task AcknowledgeAsync(PendingOperation pending, OperationResolution terminal, CancellationToken ct = default)
    {
        if (terminal.State is not (OperationResolutionState.Confirmed or OperationResolutionState.RejectedDefinitively)
            || terminal.State == OperationResolutionState.Confirmed && (terminal.Receipt is null || terminal.Receipt.OperationId != pending.Ticket.OperationId || terminal.Receipt.Id <= 0))
            throw new ArgumentException("Sólo se limpia una operación con resultado terminal.");
        await store.RemoveAsync(pending, ct);
        Recovered?.Invoke(this, new(pending, terminal));
        PendingChanged?.Invoke(this, EventArgs.Empty);
    }
}

// Isolated fallback for consumers/tests without a filesystem; never the desktop registration.
internal sealed class VolatileOperationStore : IPendingOperationStore
{
    private readonly List<PendingOperation> pending = [];
    private readonly object gate = new();
    public bool IsDurable => false;
    public Task<IReadOnlyList<PendingOperation>> LoadAsync(string scope, long userId, long? workstationId, CancellationToken ct = default)
    {
        lock (gate) return Task.FromResult<IReadOnlyList<PendingOperation>>(pending.Where(p => p.Scope == scope && p.UserId == userId && p.WorkstationId == workstationId).ToArray());
    }
    public Task<bool> TryAddAsync(PendingOperation value, CancellationToken ct = default)
    {
        lock (gate)
        {
            if (pending.Any(p => p.Scope == value.Scope && p.UserId == value.UserId && p.WorkstationId == value.WorkstationId)) return Task.FromResult(false);
            pending.Add(value); return Task.FromResult(true);
        }
    }
    public Task RemoveAsync(PendingOperation value, CancellationToken ct = default)
    {
        lock (gate) pending.RemoveAll(p => p == value);
        return Task.CompletedTask;
    }
}
