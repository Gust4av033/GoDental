using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public sealed class InventoryMovementDialogModel : ObservableObject, IDialogModel, IDisposable
{
    private readonly InventoryViewModel manager;
    private readonly long? initialLotId;
    public string Title => "Registrar movimiento · " + manager.Title;
    public string PrimaryActionLabel => "Registrar movimiento";
    public string SecondaryActionLabel => "Cancelar";
    public bool ShowPrimaryAction => true;
    public bool IsBusy => manager.IsBusy;
    public bool CanEdit => manager.CanWrite;
    public bool HasUnsavedChanges => manager.Quantity.Length > 0 || manager.UnitCost.Length > 0 ||
        manager.Reason.Length > 0 || manager.MovementType != "AJUSTE" || manager.Destination is not null ||
        manager.CreateLot || manager.LotNumber.Length > 0 || manager.Manufactured is not null ||
        manager.Expires is not null || manager.Confirmed || manager.SelectedLot?.Id != initialLotId;
    public ICommand SaveCommand => manager.SaveCommand;
    public ICommand CancelCommand { get; }
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;

    public InventoryMovementDialogModel(InventoryViewModel manager)
    {
        this.manager = manager; initialLotId = manager.SelectedLot?.Id;
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        manager.InventoryChanged += OnSaved;
        manager.PropertyChanged += OnManagerChanged;
    }

    private void OnSaved(object? sender, EventArgs e) => Saved?.Invoke(this, EventArgs.Empty);
    private void OnManagerChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanEdit));
    }
    public void Dispose()
    {
        manager.InventoryChanged -= OnSaved;
        manager.PropertyChanged -= OnManagerChanged;
    }
}
