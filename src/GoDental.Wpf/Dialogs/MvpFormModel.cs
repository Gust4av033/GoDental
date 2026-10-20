using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Wpf.ViewModels.Operations;

namespace GoDental.Wpf.Dialogs;

public sealed partial class MvpField : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public bool IsText => Kind == "text";
    public bool IsChoice => Kind == "choice";
    public bool IsDate => Kind == "date";
    public string Kind { get; }
    public bool IsMultiline => Key is "reason" or "address";
    public string DisplayMember { get; init; } = "Name";
    public IEnumerable<object> Choices { get; init; } = [];
    [ObservableProperty] private string value = "";
    [ObservableProperty] private object? selected;
    [ObservableProperty] private DateTime? date;
    public MvpField(string key, string label, string value = "", string kind = "text") { Key = key; Label = label; Value = value; Kind = kind; }
}

public sealed partial class MvpFormModel : ObservableObject, IDialogModel, IDisposable
{
    private readonly Func<MvpFormModel, Task> submit;
    public string Title { get; }
    public string PrimaryActionLabel { get; }
    public string SecondaryActionLabel => "Volver";
    public string Description { get; init; } = "Revisa la información antes de confirmar.";
    public MvpOperationModel Operation { get; }
    public ObservableCollection<MvpField> Fields { get; } = [];
    public object? Body { get; init; }
    public bool HasBody => Body is not null;
    public bool ShowFieldsFirst => Body is null;
    public bool SecretRequired { get; init; }
    public Func<string>? ReadSecret { get; set; }
    public Action? ClearSecret { get; set; }
    public bool ShowPrimaryAction => true;
    public bool IsBusy => saving || Operation.IsBusy;
    private bool requiresReview;
    public bool CanEnterSecret => !IsBusy && Operation.SessionRevision == ownerRevision;
    public bool CanEdit => !requiresReview && !IsBusy && Operation.CanStart && Operation.SessionRevision == ownerRevision;
    public bool HasUnsavedChanges => !finished;
    private readonly long ownerRevision;
    private bool saving;
    private bool finished;
    [ObservableProperty] private bool confirmed;
    [ObservableProperty] private string statusMessage = "";
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;
    public MvpFormModel(string title, string action, MvpOperationModel operation, Func<MvpFormModel, Task> submit)
    {
        ownerRevision = operation.SessionRevision; Title = title; PrimaryActionLabel = action; Operation = operation; this.submit = submit;
        SaveCommand = new AsyncRelayCommand(SaveAsync); CancelCommand = new RelayCommand(() => CancelRequested?.Invoke(this, EventArgs.Empty));
        Operation.Rejected += OnRejected;
        Operation.Committed += OnCommitted;
        Operation.PropertyChanged += OperationChanged;
    }
    private void OnRejected(object? sender, EventArgs e)
    {
        if (Operation.LastErrorCode is "CONCURRENCY_CONFLICT" or "INVALID_STATE" or "FORBIDDEN" or "INSUFFICIENT_STOCK" or "DUPLICATE")
        { requiresReview = true; StatusMessage = "Vuelve a la página, actualiza el detalle y revisa la información antes de abrir una nueva operación."; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEnterSecret)); }
    }
    private void OnCommitted(object? sender, EventArgs e) { finished = true; Saved?.Invoke(this, EventArgs.Empty); }
    private void OperationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) { OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEnterSecret)); }
    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (!Confirmed) { StatusMessage = "Marca la confirmación después de revisar los datos."; return; }
        saving = true; OperationChanged(this, new(null));
        try { await submit(this); }
        catch (ArgumentException ex) { StatusMessage = ex.Message; }
        catch (Exception) { StatusMessage = "No se pudo completar la consulta o autorización. Revisa el estado antes de continuar."; }
        finally { ClearSecret?.Invoke(); saving = false; OperationChanged(this, new(null)); }
    }
    public void Complete() { finished = true; Saved?.Invoke(this, EventArgs.Empty); }
    public string Text(string key) => Fields.Single(f => f.Key == key).Value.Trim();
    public T Choice<T>(string key) where T : class => Fields.Single(f => f.Key == key).Selected as T ?? throw new ArgumentException("Selecciona " + Fields.Single(f => f.Key == key).Label + ".");
    public decimal Number(string key)
    {
        if (!decimal.TryParse(Text(key), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out var value)) throw new ArgumentException("Revisa " + Fields.Single(f => f.Key == key).Label + ". Usa el separador decimal de tu equipo.");
        return value;
    }
    public DateTime? Date(string key) => Fields.Single(f => f.Key == key).Date?.Date;
    public MvpFormModel Input(string key, string label, string value = "") { Fields.Add(new(key, label, value)); return this; }
    public MvpFormModel Select(string key, string label, IEnumerable<object> choices, object? selected = null, string display = "Name") { Fields.Add(new(key, label, kind: "choice") { Choices = choices, Selected = selected, DisplayMember = display }); return this; }
    public MvpFormModel Calendar(string key, string label) { Fields.Add(new(key, label, kind: "date")); return this; }
    public void Dispose() { Operation.Rejected -= OnRejected; Operation.Committed -= OnCommitted; Operation.PropertyChanged -= OperationChanged; ClearSecret?.Invoke(); ReadSecret = null; ClearSecret = null; }
}

public interface IMvpDialogService { Task<bool> ShowAsync(MvpFormModel form); }
