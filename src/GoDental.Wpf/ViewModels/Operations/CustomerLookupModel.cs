using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CustomerLookupModel(CustomerService customers, Func<long> revision) : ObservableObject
{
    public ObservableCollection<CustomerData> Items { get; } = [];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private CustomerData? selected;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Busca y selecciona un cliente. Sin selección: consumidor final en POS.";
    private int page = 1, total;
    public event EventHandler? SelectionChanged;
    partial void OnSelectedChanged(CustomerData? value) => SelectionChanged?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private async Task SearchAsync() { if (IsBusy) return; page = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || page * 30 >= total) return; page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || page <= 1) return; page--; await LoadAsync(); }
    [RelayCommand] private void Clear() { Selected = null; StatusMessage = "Sin cliente seleccionado."; }
    private async Task LoadAsync()
    {
        IsBusy = true; var started = revision();
        try
        {
            var result = await customers.SearchAsync(new(Search, Page: page));
            if (started != revision()) return;
            Items.Clear(); foreach (var c in result.Items) Items.Add(c);
            total = result.Total; StatusMessage = total == 0 ? "No hay clientes para esta búsqueda." : $"{total} clientes · Página {page}";
        }
        catch (Exception) { Items.Clear(); StatusMessage = "No se pudieron consultar clientes. Revisa conexión y permisos."; }
        finally { IsBusy = false; }
    }
    public void Reset() { Selected = null; Items.Clear(); Search = ""; page = 1; total = 0; }
}
