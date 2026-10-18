using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public sealed class UserDialogModel : ObservableObject, IDialogModel, IDisposable
{
    private readonly UsersViewModel manager;
    private readonly Func<(string Password, string Confirmation)> readPasswords;
    private readonly Action clearPasswords;
    private readonly Func<bool> hasPasswords;
    private readonly bool create;
    private readonly int? initialRoleId;
    public string Title => create ? "Crear cuenta" : "Administrar cuenta";
    public string PrimaryActionLabel => "Crear usuario";
    public string SecondaryActionLabel => "Cerrar";
    public bool ShowPrimaryAction => create;
    public bool IsBusy => manager.IsBusy;
    public bool CanEdit => manager.CanEdit;
    public bool HasUnsavedChanges => create
        ? !string.IsNullOrWhiteSpace(manager.UserName) || !string.IsNullOrWhiteSpace(manager.DisplayName) ||
          manager.Role?.Id != initialRoleId || hasPasswords()
        : !string.IsNullOrWhiteSpace(manager.Reason) || hasPasswords();
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;

    public UserDialogModel(UsersViewModel manager, bool create,
        Func<(string Password, string Confirmation)> readPasswords, Action clearPasswords, Func<bool> hasPasswords)
    {
        this.manager = manager; this.create = create;
        this.readPasswords = readPasswords; this.clearPasswords = clearPasswords; this.hasPasswords = hasPasswords;
        initialRoleId = manager.Role?.Id;
        SaveCommand = new AsyncRelayCommand(CreateAsync);
        CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        manager.Saved += OnSaved;
        manager.PropertyChanged += OnManagerChanged;
    }

    private async Task CreateAsync()
    {
        if (!create || !manager.CanEdit) return;
        var (password, confirmation) = readPasswords();
        try { await manager.CreateAsync(password, confirmation); }
        finally { clearPasswords(); }
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
        clearPasswords();
    }
}
