using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class PendingOperationsViewModel : ObservableObject
{
    private readonly MvpOperationService operations;
    private readonly ISessionController session;
    private readonly ILogger logger;
    public ObservableCollection<PendingOperation> Items { get; } = [];
    public bool HasAttention => Items.Count > 0 || LoadFailed;
    [ObservableProperty] private PendingOperation? selected;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAttention))] private bool loadFailed;
    [ObservableProperty] private string statusMessage = "Selecciona Actualizar para consultar las solicitudes guardadas en este equipo.";
    public PendingOperationsViewModel(MvpOperationService operations, ISessionController session, ILogger<PendingOperationsViewModel> logger)
    {
        this.operations = operations; this.session = session; this.logger = logger;
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttention));
        session.Changed += (_, _) => { Items.Clear(); Selected = null; LoadFailed = false; if (session.IsAuthenticated) ReloadCommand.Execute(null); };
        operations.PendingChanged += (_, _) => { if (session.IsAuthenticated && !IsBusy) ReloadCommand.Execute(null); };
    }
    [RelayCommand] private async Task ReloadAsync()
    {
        if (IsBusy || !session.IsAuthenticated) return;
        IsBusy = true; var revision = session.Revision;
        try
        {
            var rows = await operations.PendingAsync();
            if (revision != session.Revision) return;
            Items.Clear(); foreach (var row in rows) Items.Add(row); Selected = Items.FirstOrDefault(); LoadFailed = false;
            StatusMessage = Items.Count == 0 ? "No hay solicitudes pendientes en esta base para tu cuenta y estación." : "Hay solicitudes guardadas. Consulta el resultado antes de capturar una nueva operación.";
        }
        catch (Exception ex) { Error(ex, revision); }
        finally { IsBusy = false; }
    }
    [RelayCommand] private Task CheckAsync() => ResolveAsync(false);
    [RelayCommand] private Task ResolveAndReleaseAsync() => ResolveAsync(true);
    private async Task ResolveAsync(bool reject)
    {
        if (IsBusy || Selected is null || !session.IsAuthenticated) return;
        IsBusy = true; var revision = session.Revision; var item = Selected;
        try
        {
            var result = await operations.ResolveAsync(item, reject);
            if (revision != session.Revision) return;
            StatusMessage = result.State switch
            {
                OperationResolutionState.Confirmed => $"Operación confirmada · Registro {result.Receipt!.Id}. Abre su módulo y actualiza para consultar el detalle.",
                OperationResolutionState.RejectedDefinitively => "Solicitud cerrada definitivamente sin confirmar. Ya puedes revisar y capturar una nueva; no se anuló una venta confirmada.",
                OperationResolutionState.InProgress => "La solicitud sigue en curso. Consulta de nuevo antes de continuar.",
                _ => "No hay confirmación registrada. Esto no es un rechazo definitivo. Puedes resolver y liberar para impedir que ese envío se confirme después."
            };
            if (result.State is OperationResolutionState.Confirmed or OperationResolutionState.RejectedDefinitively) { Items.Remove(item); Selected = Items.FirstOrDefault(); }
        }
        catch (Exception ex) { Error(ex, revision); }
        finally { IsBusy = false; }
    }
    private void Error(Exception ex, long revision)
    {
        logger.LogWarning("No se pudo recuperar diario: {ErrorType}", ex.GetType().Name);
        if (revision != session.Revision) return;
        LoadFailed = true;
        StatusMessage = "No se pudo comprobar o limpiar la solicitud. Conservamos el registro; revisa la conexión y el almacenamiento antes de continuar.";
    }
}
