using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public sealed class CategoryDialogModel : ObservableObject, IDialogModel, IDisposable
{
    private readonly CategoriesViewModel manager;
    public string Title => "Administrar categorías";
    public string PrimaryActionLabel => "Guardar categoría";
    public string SecondaryActionLabel => "Cerrar";
    public bool ShowPrimaryAction => true;
    public bool HasUnsavedChanges => manager.HasPendingChanges;
    public bool IsBusy => manager.IsBusy;
    public bool CanEdit => manager.CanEdit && manager.Selected is not null;
    public ICommand SaveCommand => manager.SaveCommand;
    public ICommand CancelCommand { get; }
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;

    public CategoryDialogModel(CategoriesViewModel manager)
    {
        this.manager = manager;
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        manager.Saved += OnSaved;
        manager.PropertyChanged += OnManagerChanged;
    }

    private void OnSaved(object? sender, EventArgs e) => Saved?.Invoke(this, EventArgs.Empty);
    private void OnManagerChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanEdit));
    }
    public void Dispose()
    {
        manager.Saved -= OnSaved;
        manager.PropertyChanged -= OnManagerChanged;
    }
}
