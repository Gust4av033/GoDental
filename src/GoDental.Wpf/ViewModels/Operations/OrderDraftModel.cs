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
    public ObservableCollection<CustomerContactData> Contacts { get; } = [];
    public ObservableCollection<CustomerAddressData> Addresses { get; } = [];
    public ObservableCollection<ProductData> Products { get; } = [];
    public ObservableCollection<CartLineModel> Items { get; } = [];
    [ObservableProperty] private CustomerAddressData? address;
    [ObservableProperty] private CustomerContactData? contact;
    [ObservableProperty] private string contactName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string deliveryAddress = "";
    [ObservableProperty] private string department = "";
    [ObservableProperty] private string municipality = "";
    [ObservableProperty] private string reference = "";
    private bool applyingAddress;
    private long customerRead;
    private CustomerData? loadedCustomer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsBusy))] private bool isLoadingCustomer;
    public bool IsBusy => IsLoadingCustomer || IsLoadingProducts;
    [ObservableProperty] private ProductData? product;
    [ObservableProperty] private CartLineModel? selectedLine;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsBusy))] private bool isLoadingProducts;
    [ObservableProperty] private string statusMessage = "Selecciona cliente, dirección de entrega y productos del pedido.";
    private int page = 1, total;
    public OrderDraftModel(CustomerService customers, ProductService products, Func<long> revision)
    {
        this.customers = customers; this.products = products; this.revision = revision; Customer = new(customers, revision);
        Customer.SelectionChanged += (_, _) => AddressesCommand.Execute(null);
    }
    private static string FirstText(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    partial void OnContactChanged(CustomerContactData? value)
    {
        ContactName = value?.Name ?? loadedCustomer?.Name ?? Customer.Selected?.Name ?? "";
        Phone = FirstText(value?.Phone, value?.WhatsApp, loadedCustomer?.Phone, loadedCustomer?.WhatsApp, Customer.Selected?.Phone, Customer.Selected?.WhatsApp);
    }
    partial void OnAddressChanged(CustomerAddressData? value)
    {
        if (value is null) return;
        applyingAddress = true;
        try { DeliveryAddress = value.Address; Department = value.Department ?? ""; Municipality = value.Municipality ?? ""; Reference = value.Reference ?? ""; }
        finally { applyingAddress = false; }
    }
    private void ManualAddress() { if (!applyingAddress) Address = null; }
    partial void OnDeliveryAddressChanged(string value) => ManualAddress();
    partial void OnDepartmentChanged(string value) => ManualAddress();
    partial void OnMunicipalityChanged(string value) => ManualAddress();
    partial void OnReferenceChanged(string value) => ManualAddress();
    private static T? Suggested<T>(IReadOnlyList<T> values, Func<T, bool> primary) where T : class
    {
        var marked = values.Where(primary).ToArray();
        return marked.Length == 1 ? marked[0] : marked.Length == 0 && values.Count == 1 ? values[0] : null;
    }
    [RelayCommand(AllowConcurrentExecutions = true)] private async Task AddressesAsync()
    {
        var read = ++customerRead; var r = revision(); var customerId = Customer.Selected?.Id;
        loadedCustomer = null; Addresses.Clear(); Contacts.Clear(); Address = null; Contact = null;
        ContactName = Customer.Selected?.Name ?? ""; Phone = FirstText(Customer.Selected?.Phone, Customer.Selected?.WhatsApp);
        DeliveryAddress = Department = Municipality = Reference = "";
        IsLoadingCustomer = customerId is not null;
        if (customerId is null) { StatusMessage = "Selecciona un cliente para cargar sus datos de entrega."; return; }
        try
        {
            var result = await customers.ReadAsync(customerId.Value);
            if (r != revision() || read != customerRead || Customer.Selected?.Id != customerId) return;
            if (result is null) { StatusMessage = "El cliente ya no está disponible. Vuelve a seleccionarlo."; return; }
            loadedCustomer = result.Customer; ContactName = result.Customer.Name; Phone = FirstText(result.Customer.Phone, result.Customer.WhatsApp);
            foreach (var c in result.Contacts.Where(c => c.Active)) Contacts.Add(c);
            foreach (var a in result.Addresses.Where(a => a.Active && a.Type == "ENTREGA")) Addresses.Add(a);
            Contact = Suggested(Contacts.ToArray(), c => c.Primary);
            Address = Suggested(Addresses.ToArray(), a => a.Primary);
            StatusMessage = Address is not null ? "Datos del cliente cargados. Revisa contacto y dirección antes de registrar."
                : Addresses.Count > 0 ? "Elige una dirección de entrega y revisa los datos del contacto."
                : "El cliente no tiene dirección de entrega guardada; completa los datos de entrega.";
        }
        catch (Exception) { if (r == revision() && read == customerRead) StatusMessage = "No se pudieron cargar los datos del cliente. Pulsa Recargar datos para reintentar."; }
        finally { if (read == customerRead) IsLoadingCustomer = false; }
    }
    [RelayCommand] private async Task SearchAsync() { if (IsBusy) return; page = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || page * 30 >= total) return; page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || page <= 1) return; page--; await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (IsBusy) return; IsLoadingProducts = true; var r = revision();
        try
        {
            var result = await products.SearchAsync(new(Search, Page: page)); if (r != revision()) return;
            Products.Clear(); Product = null;
            if (!result.IsSuccess || result.Value is null) { StatusMessage = result.Message ?? "No se pudo consultar el catálogo."; return; }
            foreach (var p in result.Value.Items) Products.Add(p); total = result.Value.Total;
            StatusMessage = total == 0 ? "No hay productos para esta búsqueda." : $"{total} productos · Página {page}";
        }
        catch (Exception) { StatusMessage = "No se pudo consultar el catálogo."; }
        finally { IsLoadingProducts = false; }
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
