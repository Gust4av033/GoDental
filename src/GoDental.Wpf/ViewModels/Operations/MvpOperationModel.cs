using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class MvpOperationModel : ObservableObject
{
    private readonly ISessionController session;
    private readonly MvpOperationService operations;
    private readonly ILogger logger;
    private Func<Task<Result<MvpReceipt>>>? send;
    private Func<MvpReceipt, Task>? completed;
    private PendingOperation? pending;
    private long revision;
    private bool uncertain;
    public long SessionRevision => session.Revision;
    public object? Request { get; private set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStart)), NotifyPropertyChangedFor(nameof(CanResolve)), NotifyPropertyChangedFor(nameof(CanClose))] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStart)), NotifyPropertyChangedFor(nameof(CanResolve)), NotifyPropertyChangedFor(nameof(CanClose))] private bool hasPending;
    [ObservableProperty] private Guid operationId;
    [ObservableProperty] private string statusMessage = "";
    public bool CanStart => !IsBusy && !HasPending;
    public bool CanResolve => !IsBusy && HasPending;
    public bool CanClose => !IsBusy && (!HasPending || operations.IsDurable);
    public string? LastErrorCode { get; private set; }
    public event EventHandler? Committed;
    public event EventHandler? Rejected;
    public MvpOperationModel(ISessionController session, MvpOperationService operations, ILogger logger)
    {
        this.session = session; this.operations = operations; this.logger = logger;
        session.Changed += (_, _) => { Clear(); StatusMessage = ""; OnPropertyChanged(nameof(SessionRevision)); };
        operations.Recovered += (_, recovered) =>
        {
            if (!IsBusy && HasPending && pending == recovered.Pending && revision == session.Revision)
                _ = ApplyExternalResolutionAsync(recovered.Resolution);
        };
    }
    public async Task StartAsync<T>(T request, Guid id, Func<T, Task<Result<MvpReceipt>>> sender, Func<MvpReceipt, Task> onSuccess) where T : notnull
    {
        if (!CanStart || !session.IsAuthenticated) return;
        IsBusy = true; var started = session.Revision; revision = started;
        try
        {
            if (OperationIdentity.Describe(request).OperationId != id) throw new ArgumentException("El identificador no corresponde a la solicitud.");
            var saved = await operations.RememberAsync(request);
            if (started != session.Revision) return;
            pending = saved; uncertain = false; LastErrorCode = null; Request = request; OperationId = id;
            send = () => sender(request); completed = onSuccess; HasPending = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("No se pudo conservar solicitud antes de enviar: {ErrorType}", ex.GetType().Name);
            if (started == session.Revision) StatusMessage = "No se envió la operación. " + (ex is InvalidOperationException or ArgumentException ? ex.Message : "No se pudo guardar el registro de recuperación. Revisa el almacenamiento local.");
        }
        finally { IsBusy = false; }
        if (started == session.Revision && HasPending) await SendAsync();
    }
    [RelayCommand] private Task RetryAsync() => CanResolve ? SendAsync() : Task.CompletedTask;
    [RelayCommand] private Task CheckAsync() => ResolveAsync(false);
    [RelayCommand] private Task ResolveAndReleaseAsync() => ResolveAsync(true);
    private async Task ResolveAsync(bool rejectIfAbsent)
    {
        if (!CanResolve || pending is null) return;
        IsBusy = true; var started = revision;
        try
        {
            var result = await operations.ResolveAsync(pending, rejectIfAbsent);
            if (started == session.Revision) await ApplyRecoveredAsync(result);
        }
        catch (Exception ex) { Unknown(ex, started); }
        finally { IsBusy = false; }
    }
    private async Task ApplyExternalResolutionAsync(OperationResolution result)
    {
        IsBusy = true; var started = revision;
        try { await ApplyRecoveredAsync(result); }
        catch (Exception ex) { Unknown(ex, started); }
        finally { IsBusy = false; }
    }
    private Task ApplyRecoveredAsync(OperationResolution result) => result.State switch
    {
        OperationResolutionState.Confirmed when result.Receipt is not null => FinishAsync(result.Receipt, acknowledged: true),
        OperationResolutionState.RejectedDefinitively => RejectRecoveredAsync(),
        _ => ShowUnresolvedAsync(result.State)
    };
    private Task RejectRecoveredAsync()
    {
        Clear(); LastErrorCode = "OPERATION_REJECTED";
        StatusMessage = "Solicitud cerrada definitivamente sin confirmar. Revisa los datos antes de iniciar otra. No se anuló ninguna venta confirmada.";
        Rejected?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    private Task ShowUnresolvedAsync(OperationResolutionState state)
    {
        StatusMessage = state == OperationResolutionState.InProgress ? "La solicitud sigue en curso. Consulta de nuevo; todavía no se puede liberar."
            : "Aún no hay resultado confirmado. Conserva la solicitud: puedes reenviarla o resolver y liberar para impedir un envío tardío.";
        return Task.CompletedTask;
    }
    private async Task SendAsync()
    {
        if (send is null || IsBusy || pending is null) return;
        IsBusy = true; var started = revision;
        try
        {
            var result = await send();
            if (started != session.Revision) return;
            if (result.IsSuccess && result.Value is not null) await FinishAsync(result.Value);
            else
            {
                if (uncertain)
                {
                    StatusMessage = (result.Message ?? "El reenvío no fue aceptado.") + " El resultado del envío anterior sigue sin confirmar. Consulta su estado o usa Resolver y liberar.";
                    return;
                }
                var terminal = await operations.ResolveAsync(pending, rejectIfAbsent: true);
                if (started != session.Revision) return;
                if (terminal.State != OperationResolutionState.RejectedDefinitively) { await ApplyRecoveredAsync(terminal); return; }
                var code = result.ErrorCode; LastErrorCode = code; Clear();
                StatusMessage = result.Message ?? "La operación fue rechazada. Revisa los datos.";
                if (code == "CONCURRENCY_CONFLICT") StatusMessage += " Actualiza el detalle y revisa la versión antes de crear una nueva solicitud.";
                Rejected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) { Unknown(ex, started); }
        finally { IsBusy = false; }
    }
    private async Task FinishAsync(MvpReceipt receipt, bool acknowledged = false)
    {
        if (receipt.OperationId != OperationId || receipt.Id <= 0) throw new InvalidOperationException("El recibo no corresponde a la solicitud.");
        var refresh = completed; var saved = pending; var started = revision; Clear();
        StatusMessage = $"Operación confirmada · Registro {receipt.Id}.";
        if (!acknowledged && saved is not null)
        {
            try { await operations.AcknowledgeConfirmedAsync(saved, receipt); }
            catch (Exception ex) { logger.LogWarning("Limpieza de diario pendiente: {ErrorType}", ex.GetType().Name); if (started == session.Revision) StatusMessage += " Falta limpiar el registro local; abre Operaciones pendientes."; }
        }
        if (started != session.Revision) return;
        try { Committed?.Invoke(this, EventArgs.Empty); if (refresh is not null) await refresh(receipt); }
        catch (Exception ex) { logger.LogWarning("Falló recarga MVP: {ErrorType}", ex.GetType().Name); if (started == session.Revision) StatusMessage += " No se pudo actualizar la consulta; pulsa Actualizar."; }
    }
    private void Unknown(Exception ex, long started)
    {
        logger.LogWarning("Resultado MVP incierto: {ErrorType}", ex.GetType().Name);
        if (started != session.Revision) return;
        uncertain = true;
        StatusMessage = "No se pudo confirmar el resultado. Consulta la operación o reenvía la misma solicitud. No registres otra operación equivalente.";
    }
    private void Clear() { Request = null; pending = null; send = null; completed = null; HasPending = false; }
}
