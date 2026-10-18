using System.Windows.Input;

namespace GoDental.Wpf.Dialogs;

public interface IDialogModel
{
    string Title { get; }
    string PrimaryActionLabel { get; }
    string SecondaryActionLabel { get; }
    ICommand SaveCommand { get; }
    ICommand CancelCommand { get; }
    bool HasUnsavedChanges { get; }
    bool IsBusy { get; }
    event EventHandler? Saved;
    event EventHandler? CancelRequested;
}
