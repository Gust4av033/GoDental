using System.Text.Json;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Infrastructure.Data;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Common;

public sealed class OperationRecoveryTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "GoDentalRecoveryTests_" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    private static UserSession Session(string scope = "test-server|test-db", int user = 1, int station = 1)
    {
        var s = new UserSession(); s.Begin(new(user, "test", "Prueba", station, [], [], false, scope)); return s;
    }
    private static OpenCashShift Request() => new(Guid.NewGuid(), 1, 10, "Motivo privado que no debe guardarse en el diario");

    [Fact] public void TicketUsesTheNormalizedRequestAndRejectsOtherContents()
    {
        var r = Request();
        Assert.Equal(OperationIdentity.Describe(r), OperationIdentity.Describe(r with { Reason = "  " + r.Reason + "  " }));
        Assert.NotEqual(OperationIdentity.Describe(r), OperationIdentity.Describe(r with { OpeningAmount = 11 }));
        Assert.True(OperationIdentity.IsValid(OperationIdentity.Describe(r)));
        Assert.False(OperationIdentity.IsValid(new(r.OperationId, "UNKNOWN", new string('A', 64))));
        Assert.False(OperationIdentity.IsValid(new(r.OperationId, "ABRIR_CAJA", new string('Z', 64))));
    }

    [Fact] public async Task RestartLoadsIdentityWithoutPayloadAndNeverAutomaticallyResends()
    {
        var repository = new MvpUiRepository(); var session = Session(); var store = new FilePendingOperationStore(folder);
        var firstService = new MvpOperationService(repository, session, store); var request = Request();
        var first = new MvpOperationModel(session, firstService, NullLogger.Instance);
        await first.StartAsync(request, request.OperationId, _ => throw new IOException("Respuesta perdida"), _ => Task.CompletedTask);
        Assert.True(first.HasPending); Assert.True(first.CanClose);
        var json = await File.ReadAllTextAsync(Path.Combine(folder, "pending-operations.json"));
        Assert.DoesNotContain(request.Reason, json); Assert.DoesNotContain("OpeningAmount", json); Assert.DoesNotContain("Password", json);
        var restarted = new MvpOperationService(repository, Session(), new FilePendingOperationStore(folder));
        var recovered = Assert.Single(await restarted.PendingAsync()); Assert.Equal(OperationIdentity.Describe(request), recovered.Ticket);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.RememberAsync(Request()));
        Assert.Equal(OperationResolutionState.Unconfirmed, (await restarted.ResolveAsync(recovered)).State);
        Assert.Single(await restarted.PendingAsync()); Assert.Empty(repository.Writes);
        Assert.Equal(OperationResolutionState.RejectedDefinitively, (await restarted.ResolveAsync(recovered, true)).State);
        Assert.Empty(await restarted.PendingAsync());
        await restarted.RememberAsync(Request()); Assert.Single(await restarted.PendingAsync());
    }

    [Fact] public async Task RestartRecoversCommittedReceiptWithoutAnotherWrite()
    {
        var repo = new MvpUiRepository(); var session = Session(); var service = new MvpOperationService(repo, session, new FilePendingOperationStore(folder));
        var request = Request(); var saved = await service.RememberAsync(request);
        var restarted = new MvpOperationService(repo, Session(), new FilePendingOperationStore(folder));
        repo.FoundReceipt = new(8, request.OperationId);
        var result = await restarted.ResolveAsync(Assert.Single(await restarted.PendingAsync()), true);
        Assert.Equal(8, result.Receipt!.Id); Assert.Empty(repo.Writes); Assert.Empty(await restarted.PendingAsync());
    }

    [Fact] public async Task DiskFailurePreventsTheFirstBusinessSendAndCorruptionIsPreserved()
    {
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "pending-operations.json"); await File.WriteAllTextAsync(path, "broken-journal");
        var session = Session(); var service = new MvpOperationService(new MvpUiRepository(), session, new FilePendingOperationStore(folder));
        var operation = new MvpOperationModel(session, service, NullLogger.Instance); var sends = 0; var request = Request();
        await operation.StartAsync(request, request.OperationId, _ => { sends++; return Task.FromResult(Result<MvpReceipt>.Success(new(1, request.OperationId))); }, _ => Task.CompletedTask);
        Assert.Equal(0, sends); Assert.False(operation.HasPending); Assert.Contains("No se envió", operation.StatusMessage);
        Assert.Equal("broken-journal", await File.ReadAllTextAsync(path));
    }

    [Fact] public async Task FileReservationIsAtomicAcrossInstancesAndScopesAreIsolated()
    {
        var a = new FilePendingOperationStore(folder); var b = new FilePendingOperationStore(folder);
        var p = new PendingOperation("server|db", 1, 1, OperationIdentity.Describe(Request()), DateTimeOffset.UtcNow);
        var results = await Task.WhenAll(a.TryAddAsync(p), b.TryAddAsync(p with { Ticket = OperationIdentity.Describe(Request()) }));
        Assert.Single(results, x => x); Assert.Single(await a.LoadAsync("server|db", 1, 1));
        Assert.Empty(await a.LoadAsync("server|db", 2, 1)); Assert.Empty(await a.LoadAsync("server|db", 1, 2)); Assert.Empty(await a.LoadAsync("server|other", 1, 1));
        Assert.True(await a.TryAddAsync(p with { Scope = "server|other", Ticket = OperationIdentity.Describe(Request()) }));
    }

    [Fact] public async Task WrongSessionCannotResolveAnOldUsersJournal()
    {
        var session = Session(); var service = new MvpOperationService(new MvpUiRepository(), session, new FilePendingOperationStore(folder));
        var saved = await service.RememberAsync(Request()); session.Begin(new(2, "another", "Otra cuenta", 1, [], [], false, "test-server|test-db"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResolveAsync(saved, true));
        Assert.Empty(await service.PendingAsync()); Assert.Single(await new FilePendingOperationStore(folder).LoadAsync(saved.Scope, 1, 1));
    }

    [Fact] public async Task InProgressDoesNotClearTheJournalOrUnlockANewCapture()
    {
        var repo = new RecoveryRepository { State = OperationResolutionState.InProgress }; var service = new MvpOperationService(repo, Session(), new FilePendingOperationStore(folder));
        var saved = await service.RememberAsync(Request());
        Assert.Equal(OperationResolutionState.InProgress, (await service.ResolveAsync(saved, true)).State);
        Assert.Single(await service.PendingAsync()); await Assert.ThrowsAsync<InvalidOperationException>(() => service.RememberAsync(Request()));
    }

    [Fact] public async Task TerminalCleanupFailureKeepsTheJournalAndConfirmedOutcome()
    {
        var session = Session(); var underlying = new FilePendingOperationStore(folder); var store = new FailingCleanup(underlying);
        var service = new MvpOperationService(new MvpUiRepository(), session, store); var operation = new MvpOperationModel(session, service, NullLogger.Instance);
        var request = Request(); var refreshes = 0;
        await operation.StartAsync(request, request.OperationId, _ => Task.FromResult(Result<MvpReceipt>.Success(new(9, request.OperationId))), _ => { refreshes++; return Task.CompletedTask; });
        Assert.False(operation.HasPending); Assert.Equal(1, refreshes); Assert.Contains("confirmada", operation.StatusMessage);
        Assert.Single(await service.PendingAsync()); await Assert.ThrowsAsync<InvalidOperationException>(() => service.RememberAsync(Request()));
    }

    [Fact] public async Task UnresolvedOperationCanBeExplicitlyFencedAndOriginalWorkspaceReleased()
    {
        var session = Session(); var service = new MvpOperationService(new MvpUiRepository(), session, new FilePendingOperationStore(folder));
        var model = new MvpOperationModel(session, service, NullLogger.Instance); var request = Request();
        await model.StartAsync(request, request.OperationId, _ => throw new IOException(), _ => Task.CompletedTask);
        await model.ResolveAndReleaseCommand.ExecuteAsync(null);
        Assert.True(model.CanStart); Assert.Equal("OPERATION_REJECTED", model.LastErrorCode); Assert.Empty(await service.PendingAsync());
    }

    [Fact] public async Task RecoveryScreenRestoresCommittedRecordAndDoesNotWriteBusinessData()
    {
        var repo = new MvpUiRepository(); var service = new MvpOperationService(repo, Session(), new FilePendingOperationStore(folder));
        var saved = await service.RememberAsync(Request());
        var session = Session(); var restarted = new MvpOperationService(repo, session, new FilePendingOperationStore(folder));
        var vm = new PendingOperationsViewModel(restarted, session, NullLogger<PendingOperationsViewModel>.Instance);
        await vm.ReloadCommand.ExecuteAsync(null); Assert.True(vm.HasAttention); Assert.Equal(saved, vm.Selected);
        repo.FoundReceipt = new(15, saved.Ticket.OperationId); await vm.CheckCommand.ExecuteAsync(null);
        Assert.Empty(vm.Items); Assert.False(vm.HasAttention); Assert.Contains("15", vm.StatusMessage); Assert.Empty(repo.Writes);
    }

    [Fact] public async Task GlobalResolutionReleasesTheOriginalWorkspaceWithoutAnotherSend()
    {
        var session = Session(); var repo = new MvpUiRepository(); var service = new MvpOperationService(repo, session, new FilePendingOperationStore(folder));
        var operation = new MvpOperationModel(session, service, NullLogger.Instance); var request = Request(); var sends = 0;
        await operation.StartAsync(request, request.OperationId, _ => { sends++; throw new IOException(); }, _ => Task.CompletedTask);
        var screen = new PendingOperationsViewModel(service, session, NullLogger<PendingOperationsViewModel>.Instance);
        await screen.ReloadCommand.ExecuteAsync(null); await screen.ResolveAndReleaseCommand.ExecuteAsync(null);
        Assert.True(operation.CanStart); Assert.Equal("OPERATION_REJECTED", operation.LastErrorCode); Assert.Equal(1, sends);
    }

    [Fact] public async Task SessionChangeDuringReceiptCleanupCannotRefreshTheNextUsersWorkspace()
    {
        var session = Session(); var store = new GatedCleanup(new FilePendingOperationStore(folder));
        var service = new MvpOperationService(new MvpUiRepository(), session, store); var model = new MvpOperationModel(session, service, NullLogger.Instance);
        var request = Request(); var refreshes = 0;
        var sending = model.StartAsync(request, request.OperationId, _ => Task.FromResult(Result<MvpReceipt>.Success(new(8, request.OperationId))), _ => { refreshes++; return Task.CompletedTask; });
        await store.Cleaning.Task; session.Clear(); store.Proceed.SetResult(); await sending;
        Assert.Equal(0, refreshes); Assert.Equal("", model.StatusMessage);
    }

    private sealed class RecoveryRepository : IMvpOperationRepository
    {
        public OperationResolutionState State { get; set; }
        public Task<MvpReceipt?> FindOperationAsync(Guid id, int userId, CancellationToken ct) => Task.FromResult<MvpReceipt?>(null);
        public Task<OperationResolution> ResolveOperationAsync(OperationTicket ticket, bool rejectIfAbsent, int userId, int? workstationId, CancellationToken ct) => Task.FromResult(new OperationResolution(State));
    }
    private sealed class FailingCleanup(IPendingOperationStore store) : IPendingOperationStore
    {
        public bool IsDurable => true;
        public Task<IReadOnlyList<PendingOperation>> LoadAsync(string scope, long userId, long? workstationId, CancellationToken ct = default) => store.LoadAsync(scope, userId, workstationId, ct);
        public Task<bool> TryAddAsync(PendingOperation pending, CancellationToken ct = default) => store.TryAddAsync(pending, ct);
        public Task RemoveAsync(PendingOperation pending, CancellationToken ct = default) => throw new IOException("Simulated disk failure");
    }

    private sealed class GatedCleanup(IPendingOperationStore store) : IPendingOperationStore
    {
        public TaskCompletionSource Cleaning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Proceed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDurable => true;
        public Task<IReadOnlyList<PendingOperation>> LoadAsync(string scope, long userId, long? workstationId, CancellationToken ct = default) => store.LoadAsync(scope, userId, workstationId, ct);
        public Task<bool> TryAddAsync(PendingOperation pending, CancellationToken ct = default) => store.TryAddAsync(pending, ct);
        public async Task RemoveAsync(PendingOperation pending, CancellationToken ct = default) { Cleaning.SetResult(); await Proceed.Task; await store.RemoveAsync(pending, ct); }
    }
}
