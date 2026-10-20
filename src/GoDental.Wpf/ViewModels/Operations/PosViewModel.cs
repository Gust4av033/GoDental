using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class PosViewModel : MvpWorkspaceViewModel
{
    private readonly SalesService sales;
    private readonly CashService cash;
    private readonly CustomerService customers;
    private readonly IMvpDialogService dialogs;
    public CustomerLookupModel Customer { get; }
    public ObservableCollection<CashBoxData> Boxes { get; } = [];
    public ObservableCollection<PosProductData> Products { get; } = [];
    public ObservableCollection<CartLineModel> Items { get; } = [];
    public ObservableCollection<PosCartRow> CartRows { get; } = [];
    public string CartCountLabel => $"{Items.Count} líneas en el carrito";
    public bool HasConfirmedSale => ConfirmedSale is not null;
    private void RefreshCartRows()
    {
        var selected = SelectedLine;
        CartRows.Clear();
        foreach (var item in Items) CartRows.Add(new(item, Quote?.Lines.FirstOrDefault(line => line.ProductId == item.ProductId)));
        SelectedLine = Items.Contains(selected!) ? selected : null;
        OnPropertyChanged(nameof(CartCountLabel));
    }
    public ObservableCollection<CatalogOption> PriceLists { get; } = [];
    [ObservableProperty] private CatalogOption? priceList;
    [ObservableProperty] private CashBoxData? box;
    [ObservableProperty] private PosProductData? product;
    [ObservableProperty] private CartLineModel? selectedLine;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string globalDiscount = "0";
    [ObservableProperty] private string deliveryCharge = "0";
    [ObservableProperty] private SaleQuote? quote;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasConfirmedSale))] private SaleSnapshot? confirmedSale;
    [ObservableProperty] private long? quotationId;
    [ObservableProperty] private long? authorizationId;
    [ObservableProperty] private int page = 1;
    [ObservableProperty] private int total;
    private IReadOnlyList<PaymentMethodData> methods = [];
    public override bool CanRead => sales.CanRead && cash.CanRead;
    public bool CanChangeCart => CanEdit && QuotationId is null && !HasConfirmedSale;
    public bool CanAddProduct => CanChangeCart && Product is not null;
    partial void OnProductChanged(PosProductData? value) => OnPropertyChanged(nameof(CanAddProduct));
    public bool CanChooseCustomer => CanChangeCart && customers.CanRead;
    public bool CanSell => CanEdit && sales.CanSell && Quote is not null;
    public bool CanQuote => CanEdit && sales.CanQuote && Quote is not null && QuotationId is null;
    public bool CanAuthorize => CanEdit && sales.CanSell && Quote is not null;
    public bool NeedsAuthorization => Quote?.Lines.Any(l => l.EffectiveDiscountPercent > Quote.AuthorizationThreshold) == true;
    public string QuoteLabel => Quote is null ? "Calcula para consultar precios, impuestos, descuentos y disponibilidad." : $"Total aceptado: {Quote.Total:N2} · Justificación > {Quote.JustificationThreshold}% · Autorización > {Quote.AuthorizationThreshold}%";
    protected override void PermissionsChanged() { foreach (var n in new[] { nameof(CanChangeCart), nameof(CanAddProduct), nameof(CanChooseCustomer), nameof(CanSell), nameof(CanQuote), nameof(CanAuthorize), nameof(NeedsAuthorization), nameof(QuoteLabel) }) OnPropertyChanged(n); }
    partial void OnQuotationIdChanged(long? value) => PermissionsChanged();
    partial void OnConfirmedSaleChanged(SaleSnapshot? value) => PermissionsChanged();
    partial void OnQuoteChanged(SaleQuote? value) { RefreshCartRows(); PermissionsChanged(); }
    partial void OnBoxChanged(CashBoxData? value) { Invalidate(); Products.Clear(); Product = null; }
    partial void OnPriceListChanged(CatalogOption? value) => Invalidate();
    partial void OnGlobalDiscountChanged(string value) => Invalidate();
    partial void OnDeliveryChargeChanged(string value) => Invalidate();
    public PosViewModel(SalesService sales, CashService cash, CustomerService customers, IMvpDialogService dialogs, ISessionController session, MvpOperationService operations, ILogger<PosViewModel> logger) : base(session, operations, logger)
    {
        this.sales = sales; this.cash = cash; this.customers = customers; this.dialogs = dialogs; Customer = new(customers, () => Session.Revision);
        Items.CollectionChanged += (_, _) => RefreshCartRows();
        Customer.SelectionChanged += (_, _) => Invalidate();
        Operation.Rejected += (_, _) => Invalidate();
    }
    private void Invalidate() { Quote = null; AuthorizationId = null; }
    protected override void Reset() { Boxes.Clear(); Products.Clear(); Items.Clear(); PriceLists.Clear(); Box = null; Product = null; SelectedLine = null; PriceList = null; Customer.Reset(); Invalidate(); ConfirmedSale = null; QuotationId = null; GlobalDiscount = DeliveryCharge = "0"; methods = []; Page = 1; Total = 0; }
    [RelayCommand] private Task ReloadAsync() => ReadAsync(async r =>
    {
        var boxes = await cash.BoxesAsync(); var payments = await sales.PaymentMethodsAsync();
        var lists = customers.CanRead ? (await customers.CatalogsAsync()).PriceLists : [];
        if (!Current(r)) return;
        var id = Box?.Id; Boxes.Clear(); foreach (var b in boxes.Where(b => b.Active)) Boxes.Add(b); Box = Boxes.FirstOrDefault(b => b.Id == id) ?? Boxes.FirstOrDefault();
        methods = payments.Where(m => m.Code != "TARJETA" && (m.Code != "CREDITO" || Has("VENTAS.USAR_CREDITO"))).ToArray();
        var listId = PriceList?.Id; PriceLists.Clear(); PriceLists.Add(new() { Id = 0, Name = "Condición del cliente / automática" }); foreach (var l in lists) PriceLists.Add(l); PriceList = PriceLists.FirstOrDefault(l => l.Id == listId) ?? PriceLists.First();
        Page = 1; await SearchCoreAsync(r);
    });
    [RelayCommand] private Task SearchAsync() { Page = 1; return ReadAsync(SearchCoreAsync); }
    private async Task SearchCoreAsync(long r)
    {
        if (Box is null) { StatusMessage = "Selecciona una caja activa; se requiere CAJA.VER para consultar su catálogo."; return; }
        var result = await sales.SearchProductsAsync(Box.Id, Search, new(Page)); if (!Current(r)) return;
        Products.Clear(); Product = null; foreach (var p in result.Items) Products.Add(p); Total = result.Total;
        StatusMessage = Total == 0 ? "No hay productos disponibles para esta búsqueda." : $"{Total} productos · Página {Page}";
    }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await ReadAsync(SearchCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await ReadAsync(SearchCoreAsync); }
    private async Task ShowAsync(MvpFormModel form) { using (form) { IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; } } }
    [RelayCommand] private Task AddItemAsync() => EditLineAsync(false);
    [RelayCommand] private Task EditItemAsync() => EditLineAsync(true);
    private async Task EditLineAsync(bool editing)
    {
        if (!CanChangeCart || editing && SelectedLine is null || !editing && Product is null) return;
        var existing = editing ? SelectedLine : Items.FirstOrDefault(i => i.ProductId == Product!.Id);
        var p = editing ? null : Product;
        await ShowAsync(new MvpFormModel("Cantidad y descuento", "Aceptar", Operation, f =>
        {
            var quantity = f.Number("quantity"); var discount = f.Number("discount");
            var line = new CartLineModel(existing?.ProductId ?? p!.Id, existing?.Sku ?? p!.Sku, existing?.Name ?? p!.Name, quantity, discount, existing?.LotId);
            if (existing is not null) Items.Remove(existing); Items.Add(line); Invalidate(); f.Complete(); return Task.CompletedTask;
        }) { Description = existing?.Name ?? p!.Name }.Input("quantity", "Cantidad", (existing?.Quantity ?? 1).ToString()).Input("discount", "Descuento de línea (%)", (existing?.DiscountPercent ?? 0).ToString()));
    }
    [RelayCommand] private void RemoveItem() { if (!CanChangeCart || SelectedLine is null) return; Items.Remove(SelectedLine); SelectedLine = null; Invalidate(); }
    [RelayCommand] private async Task SelectCustomerAsync()
    {
        if (!CanChooseCustomer) return;
        var choice = new CustomerLookupModel(customers, () => Session.Revision) { Selected = Customer.Selected };
        await ShowAsync(new MvpFormModel("Cliente de la venta", "Usar cliente", Operation, f => { Customer.Selected = choice.Selected; Invalidate(); f.Complete(); return Task.CompletedTask; }) { Body = choice });
    }
    private SaleCart Cart() => new(Box?.Id ?? 0, Customer.Selected?.Id, Items.Select(i => new SaleItem(i.ProductId, i.Quantity, i.DiscountPercent, i.LotId)).ToArray(), CheckoutModel.Parse(GlobalDiscount, "descuento global"), PriceList?.Id > 0 ? PriceList.Id : null, CheckoutModel.Parse(DeliveryCharge, "cargo de entrega"));
    [RelayCommand] private Task CalculateAsync() => ReadAsync(async r =>
    {
        if (!Operation.CanStart) return;
        Invalidate(); var result = await sales.QuoteAsync(Cart()); if (!Current(r)) return;
        Quote = result; StatusMessage = "Cálculo actualizado. Revisa las líneas antes de cobrar o emitir la cotización.";
    });
    [RelayCommand] private async Task CheckoutAsync()
    {
        if (!CanSell || Quote is null) return; var accepted = Quote; var auth = AuthorizationId; var quotation = QuotationId;
        var payment = new CheckoutModel(accepted, methods);
        await ShowAsync(new MvpFormModel("Cobrar venta", "Confirmar venta", Operation, async f =>
        {
            if (NeedsAuthorization && auth is null) throw new ArgumentException("Solicita la autorización administrativa de este cálculo antes de cobrar.");
            var r = Session.Revision; var shift = await cash.CurrentAsync(accepted.Cart.CashBoxId); if (!Current(r)) return;
            var id = Guid.NewGuid(); var request = new ConfirmSale(id, accepted.Cart, accepted.Token, payment.Snapshot(), payment.Tendered, shift?.Id, f.Text("reason"), auth, quotation);
            await Operation.StartAsync(request, id, q => sales.ConfirmAsync(q), receipt => ReadAsync(async revision =>
            {
                Items.Clear(); Invalidate(); QuotationId = null;
                var snapshot = await sales.ReadAsync(receipt.Id); if (!Current(revision)) return; ConfirmedSale = snapshot; await SearchCoreAsync(revision); StatusMessage = "Venta confirmada. Consulta el comprobante en Ventas y PDF.";
            }));
        }) { Body = payment, Description = $"Total {accepted.Total:N2}. Los importes aplicados deben cubrir exactamente el total. Revisa el efectivo recibido y las referencias." }.Input("reason", "Motivo / justificación de descuentos"));
    }
    [RelayCommand] private async Task SaveQuotationAsync()
    {
        if (!CanQuote || Quote is null) return; var accepted = Quote; var auth = AuthorizationId;
        await ShowAsync(new MvpFormModel("Emitir cotización", "Emitir", Operation, async f =>
        {
            if (NeedsAuthorization && auth is null) throw new ArgumentException("Solicita autorización para los descuentos de este cálculo.");
            var id = Guid.NewGuid(); var request = new SaveQuotation(id, accepted.Cart, accepted.Token, f.Date("until"), f.Text("reason"), auth);
            await Operation.StartAsync(request, id, q => sales.SaveQuotationAsync(q), receipt => { Invalidate(); QuotationId = receipt.Id; StatusMessage = "Cotización emitida. Vuelve a calcular para convertirla en venta, o vacía el carrito para iniciar otra operación. No reserva stock."; return Task.CompletedTask; });
        }) { Description = $"Total calculado: {accepted.Total:N2}. La cotización no reserva inventario." }.Calendar("until", "Vigencia (opcional)").Input("reason", "Motivo / justificación"));
    }
    [RelayCommand] private async Task AuthorizeAsync()
    {
        if (!CanAuthorize || Quote is null) return; var accepted = Quote;
        await ShowAsync(new MvpFormModel("Autorizar descuento", "Autorizar", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new DiscountApproval(id, accepted.Cart, accepted.Token, f.Text("reason"));
            var administrator = f.Text("user");
            await Operation.StartAsync(request, id, async q =>
            {
                try { return await sales.AuthorizeDiscountAsync(q, administrator, f.ReadSecret?.Invoke() ?? ""); }
                finally { f.ClearSecret?.Invoke(); }
            }, receipt =>
            {
                AuthorizationId = receipt.Id; StatusMessage = "Descuento autorizado para este cálculo; se consumirá una sola vez.";
                return Task.CompletedTask;
            });
        }) { SecretRequired = true, Description = "La autorización se aplica a este cálculo y no cambia la sesión del vendedor. La contraseña no se conserva." }.Input("user", "Usuario administrador").Input("reason", "Justificación"));
    }
    public Task LoadQuotationAsync(long id) => ReadAsync(async r =>
    {
        if (!Operation.CanStart || IsEditing || Items.Count > 0) throw new ArgumentException("Termina o limpia el carrito actual antes de recuperar una cotización.");
        var data = await sales.ReadQuotationAsync(id);
        if (data is null) throw new ArgumentException("No se encontró la cotización.");
        var snapshot = JsonSerializer.Deserialize<SaleQuote>(data.SnapshotJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ArgumentException("La cotización no contiene un carrito válido.");
        var box = Boxes.FirstOrDefault(b => b.Id == snapshot.Cart.CashBoxId) ?? throw new ArgumentException("Actualiza las cajas y selecciona una caja válida para la cotización.");
        var customer = snapshot.Cart.CustomerId is { } customerId ? await customers.ReadAsync(customerId) : null;
        var result = await sales.QuoteAsync(snapshot.Cart); if (!Current(r)) return;
        ConfirmedSale = null; Box = box; Customer.Selected = customer?.Customer; GlobalDiscount = snapshot.Cart.GlobalDiscountPercent.ToString(); DeliveryCharge = snapshot.Cart.DeliveryCharge.ToString(); PriceList = PriceLists.FirstOrDefault(l => l.Id == (snapshot.Cart.PriceListId ?? 0));
        Items.Clear(); foreach (var l in result.Lines) { var input = result.Cart.Items.Single(i => i.ProductId == l.ProductId); Items.Add(new(l.ProductId, l.Sku, l.Name, input.Quantity, input.DiscountPercent, input.LotId)); }
        QuotationId = id; Quote = result; StatusMessage = "Cotización recalculada. Revisa y acepta los precios actuales al confirmar la venta; requiere nueva autorización si corresponde.";
    });
    [RelayCommand] private void NewSale()
    {
        if (!CanEdit || ConfirmedSale is null) return;
        Items.Clear(); SelectedLine = null; Invalidate(); QuotationId = null; ConfirmedSale = null;
        Customer.Reset(); PriceList = PriceLists.FirstOrDefault(list => list.Id == 0);
        GlobalDiscount = DeliveryCharge = "0";
        StatusMessage = "Nueva venta lista.";
    }
    [RelayCommand] private async Task ClearCartAsync()
    {
        if (!CanEdit) return;
        await ShowAsync(new MvpFormModel("Vaciar carrito", "Vaciar", Operation, f => { Items.Clear(); Invalidate(); QuotationId = null; ConfirmedSale = null; f.Complete(); return Task.CompletedTask; }) { Description = "Se eliminarán las líneas del carrito actual." });
    }
}

// Presentación del carrito: importa los valores del cálculo aceptado, sin recalcularlos.
public sealed record PosCartRow(CartLineModel Input, CalculatedPrice? Calculation)
{
    public string Name => Calculation?.Name ?? Input.Name;
    public decimal Quantity => Calculation?.Quantity ?? Input.Quantity;
}
