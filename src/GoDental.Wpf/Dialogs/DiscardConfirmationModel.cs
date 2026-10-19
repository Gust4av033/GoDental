using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace GoDental.Wpf.Dialogs;

internal sealed class DiscardConfirmationModel : IDialogModel
{
    public string Title => "Descartar cambios";
    public string PrimaryActionLabel => "Descartar";
    public string SecondaryActionLabel => "Volver";
    public bool ShowPrimaryAction => true;
    public bool CanEdit => true;
    public bool HasUnsavedChanges => false;
    public bool IsBusy => false;
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;

    public DiscardConfirmationModel()
    {
        SaveCommand = new RelayCommand(() => Saved?.Invoke(this, EventArgs.Empty));
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
    }
}
