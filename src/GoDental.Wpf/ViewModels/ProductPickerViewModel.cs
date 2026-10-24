using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ProductChoice : ObservableObject
{
    public ProductData Product { get; }
    [ObservableProperty] private bool selected;
    public ProductChoice(ProductData product, bool selected) { Product = product; Selected = selected; }
}

public sealed partial class ProductPickerViewModel(ProductService products) : ObservableObject
{
    private readonly HashSet<int> selected = [];
    public ObservableCollection<ProductChoice> Items { get; } = [];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string statusMessage = "Busca productos por SKU o nombre.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsNotBusy))] private bool isBusy;
    public bool IsNotBusy => !IsBusy;
    public int SelectedCount => selected.Count;
    [ObservableProperty] private int page = 1;
    private int total;
    public IReadOnlyList<int> SelectedIds => selected.ToArray();
    [RelayCommand] private async Task SearchAsync() { if (IsBusy) return; Page = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= total) return; Page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await LoadAsync(); }
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await products.SearchAsync(new(Search, Page: Page));
            Items.Clear();
            if (!result.IsSuccess) { StatusMessage = result.Message ?? "No se pudo buscar."; return; }
            total = result.Value!.Total;
            foreach (var p in result.Value.Items)
            {
                var choice = new ProductChoice(p, selected.Contains(p.Id));
                choice.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ProductChoice.Selected)) { if (choice.Selected) selected.Add(p.Id); else selected.Remove(p.Id); OnPropertyChanged(nameof(SelectedCount)); } };
                Items.Add(choice);
            }
            StatusMessage = $"{total} productos · Página {Page}. La selección se conserva entre páginas.";
        }
        catch (Exception) { Items.Clear(); StatusMessage = "No se pudo consultar el catálogo. Revisa la conexión."; }
        finally { IsBusy = false; }
    }
}
