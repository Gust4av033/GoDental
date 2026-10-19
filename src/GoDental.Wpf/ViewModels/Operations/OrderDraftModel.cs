using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class OrderDraftModel : ObservableObject
{
    private readonly ProductService products;
    private readonly CustomerService customers;
    private readonly Func<long> revision;
    public CustomerLookupModel Customer { get; }
    public ObservableCollection<CustomerAddressData> Addresses { get; } = [];
    public ObservableCollection<ProductData> Products { get; } = [];
    public ObservableCollection<CartLineModel> Items { get; } = [];
    [ObservableProperty] private CustomerAddressData? address;
    [ObservableProperty] private ProductData? product;
    [ObservableProperty] private CartLineModel? selectedLine;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Selecciona cliente, dirección de entrega y productos del pedido.";
    private int page = 1, total;
    public OrderDraftModel(CustomerService customers, ProductService products, Func<long> revision)
    {
        this.customers = customers; this.products = products; this.revision = revision; Customer = new(customers, revision);
        Customer.SelectionChanged += (_, _) => { Addresses.Clear(); Address = null; };
    }
    [RelayCommand] private async Task AddressesAsync()
    {
        if (IsBusy || Customer.Selected is null) return; var customerId = Customer.Selected.Id; var r = revision(); IsBusy = true;
        try
        {
            var result = await customers.ReadAsync(customerId); if (r != revision() || Customer.Selected?.Id != customerId) return;
            Addresses.Clear(); foreach (var a in result?.Addresses.Where(a => a.Active && a.Type == "ENTREGA") ?? []) Addresses.Add(a);
            Address = Addresses.FirstOrDefault(a => a.Primary) ?? Addresses.FirstOrDefault(); StatusMessage = Addresses.Count == 0 ? "No hay direcciones de entrega; puedes escribir una dirección en Datos de entrega." : "Selecciona la dirección que se conservará en el pedido.";
        }
        catch (Exception) { StatusMessage = "No se pudieron consultar las direcciones."; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private async Task SearchAsync() { if (IsBusy) return; page = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || page * 30 >= total) return; page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || page <= 1) return; page--; await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (IsBusy) return; IsBusy = true; var r = revision();
        try
        {
            var result = await products.SearchAsync(new(Search, Page: page)); if (r != revision()) return;
            Products.Clear(); Product = null;
            if (!result.IsSuccess || result.Value is null) { StatusMessage = result.Message ?? "No se pudo consultar el catálogo."; return; }
            foreach (var p in result.Value.Items) Products.Add(p); total = result.Value.Total;
            StatusMessage = total == 0 ? "No hay productos para esta búsqueda." : $"{total} productos · Página {page}";
        }
        catch (Exception) { StatusMessage = "No se pudo consultar el catálogo."; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private void Add()
    {
        if (Product is null || IsBusy) return;
        try
        {
            var quantity = CheckoutModel.Parse(Quantity, "cantidad"); if (quantity <= 0) throw new ArgumentException("La cantidad debe ser positiva.");
            var prior = Items.FirstOrDefault(l => l.ProductId == Product.Id); if (prior is not null) Items.Remove(prior);
            Items.Add(new(Product.Id, Product.Sku, Product.Name, quantity, 0)); StatusMessage = "Producto agregado. Si ya existía, se reemplazó su cantidad.";
        }
        catch (ArgumentException ex) { StatusMessage = ex.Message; }
    }
    [RelayCommand] private void Remove() { if (SelectedLine is not null) Items.Remove(SelectedLine); }
}
