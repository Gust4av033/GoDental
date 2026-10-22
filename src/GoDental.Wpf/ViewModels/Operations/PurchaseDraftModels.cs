using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;

namespace GoDental.Wpf.ViewModels.Operations;
public sealed record PurchaseDraftLine(int ProductId, string Sku, string Name, decimal Quantity, decimal UnitCost, decimal Discount);
public sealed partial class PurchaseOrderDraftModel : ObservableObject
{
    private readonly PurchaseService service;
    private readonly Func<long> revision;
    public PurchaseCatalogs Catalogs { get; }
    public ObservableCollection<ProductData> Products { get; } = [];
    public ObservableCollection<PurchaseDraftLine> Items { get; } = [];
    [ObservableProperty] private CatalogOption? supplier;
    [ObservableProperty] private PurchaseCurrency? currency;
    [ObservableProperty] private DateTime? date = DateTime.Today;
    [ObservableProperty] private string rate = "1";
    [ObservableProperty] private string search = "";
    [ObservableProperty] private ProductData? product;
    [ObservableProperty] private PurchaseDraftLine? selected;
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private string unitCost = "0";
    [ObservableProperty] private string discount = "0";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Busca productos y agrega las cantidades y costos en la moneda de la orden.";
    private int page = 1, total;
    public PurchaseOrderDraftModel(PurchaseService service, Func<long> revision, PurchaseCatalogs catalogs, PurchaseOrderSnapshot? original)
    {
        this.service = service; this.revision = revision; Catalogs = catalogs;
        Supplier = catalogs.Suppliers.FirstOrDefault(s => s.Id == original?.Order.SupplierId);
        Currency = catalogs.Currencies.FirstOrDefault(c => c.Id == original?.Order.CurrencyId) ?? catalogs.Currencies.FirstOrDefault(c => c.Code == "USD");
        if (original is not null)
        {
            Date = original.Order.Date; Rate = original.Order.EstimatedRate.ToString("0.########");
            foreach (var line in original.Lines) Items.Add(new(line.ProductId, line.Sku, line.Name, line.Quantity, line.UnitCost, line.Discount));
        }
    }
    partial void OnCurrencyChanged(PurchaseCurrency? value) { if (value?.Code == "USD") Rate = "1"; }
    [RelayCommand] private async Task SearchAsync() { if (IsBusy) return; page = 1; await LoadAsync(); }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || page * 30 >= total) return; page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || page <= 1) return; page--; await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (IsBusy) return; IsBusy = true; var started = revision();
        try
        {
            var rows = await service.ProductsAsync(Search, new(page));
            if (started != revision()) return;
            Products.Clear(); foreach (var item in rows.Items) Products.Add(item);
            total = rows.Total; Product = Products.FirstOrDefault(); StatusMessage = $"{total} productos · Página {page}. Los costos se capturan en la moneda seleccionada.";
        }
        catch (Exception) { if (started == revision()) StatusMessage = "No se pudieron consultar los productos. Reintenta la búsqueda."; }
        finally { IsBusy = false; }
    }
    [RelayCommand] private void Add()
    {
        try
        {
            if (IsBusy || Product is null) return;
            var qty = CheckoutModel.Parse(Quantity, "cantidad"); var cost = CheckoutModel.Parse(UnitCost, "costo unitario"); var amount = CheckoutModel.Parse(Discount, "descuento monetario");
            if (!MvpValidator.Quantity(qty) || !PurchaseValidator.Cost(cost) || !MvpValidator.Money(amount) || amount > qty * cost) throw new ArgumentException("Revisa cantidad, costo y descuento. El descuento es un importe en moneda origen.");
            var previous = Items.FirstOrDefault(x => x.ProductId == Product.Id);
            if (previous is null && Items.Count >= 100) throw new ArgumentException("Usa hasta 100 productos.");
            if (previous is not null) Items.Remove(previous);
            Items.Add(new(Product.Id, Product.Sku, Product.Name, qty, cost, amount)); StatusMessage = "Línea agregada. Volver a agregar el mismo producto reemplaza su captura.";
        }
        catch (ArgumentException ex) { StatusMessage = ex.Message; }
    }
    [RelayCommand] private void Remove() { if (!IsBusy && Selected is { } line) Items.Remove(line); }
    public IReadOnlyList<PurchaseLine> Snapshot() => Items.Select(l => new PurchaseLine(l.ProductId, l.Quantity, l.UnitCost, l.Discount)).ToArray();
}
public sealed record ReceivingDraftLine(string Product, ReceivePurchaseLine Line)
{
    public decimal Quantity => Line.Quantity;
    public string? Lot => Line.LotNumber;
    public decimal ExtraCost => Line.ExtraUnitCostUsd;
}
public sealed partial class PurchaseReceiptDraftModel : ObservableObject
{
    public PurchaseOrderSnapshot Source { get; }
    public IReadOnlyList<CatalogOption> Warehouses { get; }
    public IReadOnlyList<PurchaseOrderLineData> PendingLines { get; }
    public ObservableCollection<ReceivingDraftLine> Items { get; } = [];
    [ObservableProperty] private CatalogOption? warehouse;
    [ObservableProperty] private PurchaseOrderLineData? orderLine;
    [ObservableProperty] private ReceivingDraftLine? selected;
    [ObservableProperty] private string rate;
    [ObservableProperty] private string quantity = "1";
    [ObservableProperty] private string extraCost = "0";
    [ObservableProperty] private string lot = "";
    [ObservableProperty] private DateTime? manufactured;
    [ObservableProperty] private DateTime? expires;
    [ObservableProperty] private bool costsComplete;
    [ObservableProperty] private string statusMessage = "Captura sólo unidades físicamente recibidas. Puedes dividir un producto en varios lotes.";
    public bool CanEditCosts { get; }
    public PurchaseReceiptDraftModel(PurchaseOrderSnapshot source, IReadOnlyList<CatalogOption> warehouses, ImportationData? import = null)
    {
        CanEditCosts = import is null; CostsComplete = import?.CostsClosed ?? false;
        Source = source; Warehouses = warehouses; Warehouse = warehouses.FirstOrDefault();
        PendingLines = source.Lines.Where(l => l.Pending > 0).ToArray(); OrderLine = PendingLines.FirstOrDefault(); Rate = (import?.Rate ?? source.Order.EstimatedRate).ToString("0.########");
    }
    [RelayCommand] private void Add()
    {
        try
        {
            if (OrderLine is null) return;
            var qty = CheckoutModel.Parse(Quantity, "cantidad recibida"); var extra = CheckoutModel.Parse(ExtraCost, "gasto adicional USD por unidad");
            if (!MvpValidator.Quantity(qty) || !PurchaseValidator.Cost(extra) || Items.Count >= 100 || Items.Where(l => l.Line.OrderLineId == OrderLine.Id).Sum(l => l.Quantity) + qty > OrderLine.Pending) throw new ArgumentException("Revisa cantidad y gasto unitario. No excedas el pendiente de la línea.");
            if (OrderLine.TracksLot && string.IsNullOrWhiteSpace(Lot) || OrderLine.TracksExpiry && Expires is null) throw new ArgumentException("Completa el lote y vencimiento que requiere el producto.");
            var line = new ReceivePurchaseLine(OrderLine.Id, qty, extra, OrderLine.TracksLot ? Lot.Trim() : null, OrderLine.TracksLot ? Expires?.Date : null, OrderLine.TracksLot ? Manufactured?.Date : null);
            Items.Add(new($"{OrderLine.Sku} · {OrderLine.Name}", line)); StatusMessage = "Línea agregada. El servidor volverá a validar pendiente y costo al aplicar.";
        }
        catch (ArgumentException ex) { StatusMessage = ex.Message; }
    }
    [RelayCommand] private void Remove() { if (Selected is { } line) Items.Remove(line); }
    public IReadOnlyList<ReceivePurchaseLine> Snapshot() => Items.Select(l => l.Line with { }).ToArray();
}
