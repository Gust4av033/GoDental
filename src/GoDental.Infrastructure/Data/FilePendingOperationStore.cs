using System.Text.Json;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Data;

public sealed class FilePendingOperationStore(string folder) : IPendingOperationStore
{
    public bool IsDurable => true;
    private string Journal => Path.Combine(folder, "pending-operations.json");
    private static bool SameOwner(PendingOperation p, string scope, long userId, long? workstationId) => p.Scope == scope && p.UserId == userId && p.WorkstationId == workstationId;

    private async Task<FileStream> LockAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        for (var i = 0; ; i++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(folder, "pending-operations.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (i < 99) { await Task.Delay(50, ct); }
        }
    }

    private async Task<List<PendingOperation>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(Journal)) return [];
        if (new FileInfo(Journal).Length > 8_388_608) throw new InvalidDataException("El diario requiere revisión; no se reemplazó.");
        await using var stream = new FileStream(Journal, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var rows = await JsonSerializer.DeserializeAsync<List<PendingOperation>>(stream, cancellationToken: ct)
            ?? throw new InvalidDataException("El diario no contiene una lista válida.");
        if (rows.Count > 10000 || rows.Any(p => p is null || string.IsNullOrWhiteSpace(p.Scope) || p.Scope.Length > 300 || p.UserId <= 0 || p.WorkstationId is null or <= 0 || p.Ticket is null || !OperationIdentity.IsValid(p.Ticket))
            || rows.Select(p => p.Ticket.OperationId).Distinct().Count() != rows.Count)
            throw new InvalidDataException("El diario contiene una solicitud inválida; no se reemplazó.");
        return rows;
    }

    private async Task WriteAsync(List<PendingOperation> rows, CancellationToken ct)
    {
        var temporary = Journal + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, rows, cancellationToken: ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, Journal, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<IReadOnlyList<PendingOperation>> LoadAsync(string scope, long userId, long? workstationId, CancellationToken ct = default)
    {
        await using var lease = await LockAsync(ct);
        return (await ReadAsync(ct)).Where(p => SameOwner(p, scope, userId, workstationId)).ToArray();
    }

    public async Task<bool> TryAddAsync(PendingOperation pending, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pending.Scope) || pending.Scope.Length > 300 || pending.UserId <= 0 || pending.WorkstationId is null or <= 0 || !OperationIdentity.IsValid(pending.Ticket))
            throw new ArgumentException("Identidad de pendiente inválida.");
        await using var lease = await LockAsync(ct);
        var rows = await ReadAsync(ct);
        if (rows.Any(p => SameOwner(p, pending.Scope, pending.UserId, pending.WorkstationId) || p.Ticket.OperationId == pending.Ticket.OperationId)) return false;
        rows.Add(pending);
        await WriteAsync(rows, ct);
        return true;
    }

    public async Task RemoveAsync(PendingOperation pending, CancellationToken ct = default)
    {
        await using var lease = await LockAsync(ct);
        var rows = await ReadAsync(ct);
        rows.RemoveAll(p => p == pending);
        await WriteAsync(rows, ct);
    }
}
