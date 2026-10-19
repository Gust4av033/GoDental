using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GoDental.Wpf.Dialogs;

// El formulario es una copia aislada; cerrar nunca modifica el agregado consultado.
public abstract partial class OperationDialogModel : ObservableObject, IDialogModel
{
    private readonly Func<OperationDialogModel, Task<bool>> save;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit))] private bool canSubmit = true;
    public bool CanEdit => !IsBusy && CanSubmit;
    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private string reason = "";
    [ObservableProperty] private bool confirmed;
    public abstract string Title { get; }
    public string PrimaryActionLabel => "Guardar";
    public string SecondaryActionLabel => "Cancelar";
    public bool ShowPrimaryAction => true;
    public bool HasUnsavedChanges => true;
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;
    public ICommand CancelCommand { get; }
    public ICommand SaveCommand { get; }
    protected OperationDialogModel(Func<OperationDialogModel, Task<bool>> save)
    {
        this.save = save;
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }
    private async Task SaveAsync()
    {
        if (IsBusy || !CanSubmit) return;
        if (!Confirmed) { StatusMessage = "Revisa los datos y marca la confirmación."; return; }
        IsBusy = true;
        try { if (await save(this)) Saved?.Invoke(this, EventArgs.Empty); }
        finally { IsBusy = false; }
    }
}
