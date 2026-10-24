using CommunityToolkit.Mvvm.ComponentModel;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public abstract partial class MvpWorkspaceViewModel : ObservableObject
{
    protected readonly ISessionController Session;
    private readonly ILogger logger;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit)), NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    public bool IsNotBusy => !IsBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool isEditing;
    [ObservableProperty] private string statusMessage = "Selecciona Actualizar para consultar.";
    public MvpOperationModel Operation { get; }
    public bool CanEdit => !IsBusy && !IsEditing && Operation.CanStart && CanRead;
    public abstract bool CanRead { get; }
    protected abstract void Reset();
    protected virtual void PermissionsChanged() { }
    protected MvpWorkspaceViewModel(ISessionController session, MvpOperationService operations, ILogger logger)
    {
        Session = session; this.logger = logger; Operation = new(session, operations, logger);
        Operation.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(CanEdit)); PermissionsChanged(); };
        session.Changed += (_, _) => { Reset(); StatusMessage = ""; OnPropertyChanged(nameof(CanRead)); OnPropertyChanged(nameof(CanEdit)); PermissionsChanged(); };
    }
    partial void OnIsBusyChanged(bool value) => PermissionsChanged();
    partial void OnIsEditingChanged(bool value) => PermissionsChanged();
    public async Task ReadAsync(Func<long, Task> action)
    {
        if (IsBusy || !CanRead) return;
        IsBusy = true; var revision = Session.Revision;
        try { await action(revision); }
        catch (Exception ex)
        {
            logger.LogWarning("Falló consulta MVP: {ErrorType}", ex.GetType().Name);
            if (!Current(revision)) return;
            StatusMessage = ex is ArgumentException ? ex.Message : ex is UnauthorizedAccessException ? "Tu sesión no tiene permiso para esta consulta." : "No se pudo actualizar. Revisa la conexión y vuelve a consultar.";
        }
        finally { IsBusy = false; }
    }
    protected bool Current(long revision) => revision == Session.Revision;
    protected bool Has(params string[] permissions) => Session.IsAuthenticated && permissions.All(Session.Permissions.Contains);
}
