using CommunityToolkit.Mvvm.ComponentModel;
using GoDental.Application.Abstractions.Security;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public abstract partial class WorkflowViewModel : ObservableObject
{
    protected readonly ISessionController Session;
    private readonly ILogger logger;
    private CancellationTokenSource? reading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "";
    public bool IsNotBusy => !IsBusy;
    protected abstract bool CanAccess { get; }
    protected abstract void Reset();
    protected virtual void RefreshPermissions() { }
    protected WorkflowViewModel(ISessionController session, ILogger logger)
    {
        Session = session; this.logger = logger;
        session.Changed += (_, _) => { reading?.Cancel(); Reset(); RefreshPermissions(); };
    }
    protected async Task RunAsync(Func<long, CancellationToken, Task> action)
    {
        if (IsBusy || !CanAccess) return;
        using var cancellation = new CancellationTokenSource();
        reading = cancellation; IsBusy = true; var revision = Session.Revision;
        try { await action(revision, cancellation.Token); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning("Falló flujo de operación: {ErrorType}.", ex.GetType().Name);
            if (revision != Session.Revision) return;
            Reset();
            if (ex is UnauthorizedAccessException) { Session.Clear(); return; }
            StatusMessage = ex is ArgumentException ? ex.Message : "No se pudo confirmar el estado de la operación. Revisa la conexión y consulta el detalle o listado antes de repetir un guardado.";
        }
        finally { reading = null; IsBusy = false; RefreshPermissions(); }
    }
    partial void OnIsBusyChanged(bool value) => RefreshPermissions();
    protected bool Current(long revision) => revision == Session.Revision;
}
