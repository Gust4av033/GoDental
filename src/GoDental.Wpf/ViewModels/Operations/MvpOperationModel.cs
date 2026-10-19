using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

// The exact request and sender survive transport errors. Never rebuild from edited fields on retry.
public sealed partial class MvpOperationModel : ObservableObject
{
    private readonly ISessionController session;
    private readonly MvpOperationService operations;
    private readonly ILogger logger;
    private Func<Task<Result<MvpReceipt>>>? send;
    private Func<MvpReceipt, Task>? completed;
    private long revision;
    public long SessionRevision => session.Revision;
    public object? Request { get; private set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStart)), NotifyPropertyChangedFor(nameof(CanResolve))] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStart)), NotifyPropertyChangedFor(nameof(CanResolve))] private bool hasPending;
    [ObservableProperty] private Guid operationId;
    [ObservableProperty] private string statusMessage = "";
    public bool CanStart => !IsBusy && !HasPending;
    public bool CanResolve => !IsBusy && HasPending;
    public string? LastErrorCode { get; private set; }
    public event EventHandler? Committed;
    public event EventHandler? Rejected;
    public MvpOperationModel(ISessionController session, MvpOperationService operations, ILogger logger)
    {
        this.session = session; this.operations = operations; this.logger = logger;
        session.Changed += (_, _) => { Clear(); StatusMessage = ""; OnPropertyChanged(nameof(SessionRevision)); };
    }
    public async Task StartAsync<T>(T request, Guid id, Func<T, Task<Result<MvpReceipt>>> sender, Func<MvpReceipt, Task> onSuccess) where T : notnull
    {
        if (!CanStart || !session.IsAuthenticated) return;
        LastErrorCode = null; Request = request; OperationId = id; revision = session.Revision;
        send = () => sender(request); completed = onSuccess; HasPending = true;
        await SendAsync();
    }
    [RelayCommand] private Task RetryAsync() => CanResolve ? SendAsync() : Task.CompletedTask;
    [RelayCommand] private async Task CheckAsync()
    {
        if (!CanResolve) return;
        IsBusy = true; var started = revision;
        try
        {
            var receipt = await operations.FindAsync(OperationId);
            if (started != session.Revision) return;
            if (receipt is null) StatusMessage = "Aún no hay resultado confirmado. Esto no garantiza que el envío haya terminado. Conserva esta operación; puedes reenviar la misma solicitud.";
            else await FinishAsync(receipt);
        }
        catch (Exception ex) { Unknown(ex, started); }
        finally { IsBusy = false; }
    }
    private async Task SendAsync()
    {
        if (send is null || IsBusy) return;
        IsBusy = true; var started = revision;
        try
        {
            var result = await send();
            if (started != session.Revision) return;
            if (result.IsSuccess && result.Value is not null) await FinishAsync(result.Value);
            else
            {
                var code = result.ErrorCode; LastErrorCode = code; Clear();
                StatusMessage = result.Message ?? "La operación fue rechazada. Revisa los datos.";
                if (code == "CONCURRENCY_CONFLICT") StatusMessage += " Actualiza el detalle y revisa la versión antes de crear una nueva solicitud.";
                Rejected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) { Unknown(ex, started); }
        finally { IsBusy = false; }
    }
    private async Task FinishAsync(MvpReceipt receipt)
    {
        var refresh = completed; Clear();
        StatusMessage = $"Operación confirmada · Registro {receipt.Id}.";
        Committed?.Invoke(this, EventArgs.Empty);
        // Refresh is deliberately outside the write outcome: a read failure cannot reopen a committed write.
        try { if (refresh is not null) await refresh(receipt); }
        catch (Exception ex) { logger.LogWarning("Falló recarga MVP: {ErrorType}", ex.GetType().Name); StatusMessage += " No se pudo actualizar la consulta; pulsa Actualizar."; }
    }
    private void Unknown(Exception ex, long started)
    {
        logger.LogWarning("Resultado MVP incierto: {ErrorType}", ex.GetType().Name);
        if (started != session.Revision) return;
        StatusMessage = "No se pudo confirmar el resultado. Consulta la operación o reenvía la misma solicitud. No registres otra operación equivalente.";
    }
    private void Clear() { Request = null; send = null; completed = null; HasPending = false; }
}
