using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;
namespace GoDental.Wpf.ViewModels;
public sealed partial class CategoriesViewModel : ObservableObject
{
    public event EventHandler? Saved;
    private readonly CategoryService service;
    private readonly ProductService? products;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPendingChanges)), NotifyPropertyChangedFor(nameof(CanChangeSelection))] private bool isCreating;
    public bool CanCreate => CanEdit && products?.CanManage == true;
    public bool CanSetActive => CanEdit && !IsCreating;
    private readonly ISessionController session;
    private readonly ILogger<CategoriesViewModel> logger;
    public ObservableCollection<CategoryData> Items { get; } = [];
    [ObservableProperty] private CategoryData? selected;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPendingChanges))] [NotifyPropertyChangedFor(nameof(CanChangeSelection))] private string name = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPendingChanges))] [NotifyPropertyChangedFor(nameof(CanChangeSelection))] private bool active;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEdit)), NotifyPropertyChangedFor(nameof(CanChangeSelection))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Actualiza para crear o administrar categorías.";
    public bool CanEdit => service.CanManage && !IsBusy;
    public bool HasPendingChanges => IsCreating || Selected is { } row && (Name != row.Name || Active != row.Active);
    public bool CanChangeSelection => !IsBusy && !HasPendingChanges;
    public bool IsEmpty => Items.Count == 0;
    public CategoriesViewModel(CategoryService service, ISessionController session, ILogger<CategoriesViewModel> logger, ProductService? products = null)
    {
        this.products=products; this.service=service; this.session=session; this.logger=logger;
        session.Changed += (_, _) => { Items.Clear(); IsCreating=false; Selected=null; Name=""; Active=false; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(CanSetActive)); OnPropertyChanged(nameof(IsEmpty)); };
    }
    partial void OnSelectedChanged(CategoryData? value) { if(value is not null) IsCreating=false; Name=value?.Name ?? ""; Active=value?.Active ?? false; OnPropertyChanged(nameof(HasPendingChanges)); OnPropertyChanged(nameof(CanChangeSelection)); }
    [RelayCommand] private void DiscardChanges()
    {
        if (IsBusy) return;
        IsCreating = false;
        Name = Selected?.Name ?? ""; Active = Selected?.Active ?? false;
        StatusMessage = "Cambios del formulario descartados.";
    }
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanCreate)); OnPropertyChanged(nameof(CanSetActive)); }
    partial void OnIsCreatingChanged(bool value) => OnPropertyChanged(nameof(CanSetActive));
    [RelayCommand] private void NewCategory()
    {
        if (!CanCreate || HasPendingChanges) return;
        Selected = null; IsCreating = true; Name = ""; Active = true;
        StatusMessage = "Nueva categoría: escribe el nombre y guarda.";
    }
    public async Task ReloadAsync() => await ExecuteAsync(async revision =>
    {
        var rows=await service.ListAsync(); if (revision!=session.Revision) return;
        Items.Clear(); IsCreating=false; foreach(var row in rows) Items.Add(row); Selected=null;
        OnPropertyChanged(nameof(IsEmpty));
        StatusMessage="Selecciona una categoría para cambiar su nombre o estado.";
    });
    [RelayCommand] private async Task SaveAsync()
    {
        if(!IsCreating && Selected is null) { StatusMessage="Selecciona una categoría."; return; }
        var creating = IsCreating;
        var categoryName = Name;
        var change = Selected is null ? null : new CategoryChange(Selected.Id,Name,Active,Selected.Name,Selected.Active);
        var saved = false;
        await ExecuteAsync(async revision =>
        {
            var result = creating && products is not null ? await products.CreateCategoryAsync(categoryName) : change is not null ? await service.SaveAsync(change) : GoDental.Application.Common.Result<int>.Failure("FORBIDDEN", "No se puede crear la categoría."); if(revision!=session.Revision) return;
            StatusMessage=result.IsSuccess ? "Categoría guardada. Actualiza el catálogo de productos para ver el cambio." : result.Message ?? "No se pudo guardar.";
            if(result.IsSuccess) { IsCreating=false; Selected=null; Items.Clear(); OnPropertyChanged(nameof(IsEmpty)); saved = true; }
        });
        if (saved) Saved?.Invoke(this, EventArgs.Empty);
    }
    private async Task ExecuteAsync(Func<long,Task> action)
    {
        if(!CanEdit) return; IsBusy=true; var revision=session.Revision;
        try { await action(revision); }
        catch(Exception ex)
        {
            logger.LogWarning("Falló categorías: {ErrorType}.",ex.GetType().Name);
            if(revision==session.Revision) StatusMessage="No se pudo completar la operación. Actualiza antes de repetir.";
        }
        finally { IsBusy=false; }
    }
}
