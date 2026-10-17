using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly ProductService service;
    private readonly ISessionController session;
    private readonly ILogger<ProductsViewModel> logger;
    private ProductData editing = new();
    public ObservableCollection<ProductData> Products { get; } = [];
    public ObservableCollection<CatalogOption> Categories { get; } = [];
    public ObservableCollection<CatalogOption> Brands { get; } = [];
    public ObservableCollection<CatalogOption> Units { get; } = [];
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool includeInactive;
    [ObservableProperty] private ProductData? selectedProduct;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Busca o registra tu primer producto.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PageLabel))] [NotifyPropertyChangedFor(nameof(IsEmpty))] private int total;
    [ObservableProperty] private string sku = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string price = "0.00";
    [ObservableProperty] private string minimumStock = "0";
    [ObservableProperty] private string maximumStock = "";
    [ObservableProperty] private CatalogOption? category;
    [ObservableProperty] private CatalogOption? brand;
    [ObservableProperty] private CatalogOption? unit;
    [ObservableProperty] private bool tracksLot;
    [ObservableProperty] private bool tracksExpiry;
    [ObservableProperty] private bool active = true;
    [ObservableProperty] private string newCategory = "";
    [ObservableProperty] private string editorTitle = "Nuevo producto";
    public bool CanView => service.CanRead;
    public bool CanManage => service.CanManage;
    public bool CanEdit => CanManage && !IsBusy;
    public bool IsNotBusy => !IsBusy;
    public bool IsEmpty => Total == 0;
    public string PageLabel => $"{Total} productos · Página {Page} de {Math.Max(1, (int)Math.Ceiling(Total / 30d))}";

    public ProductsViewModel(ProductService service, ISessionController session, ILogger<ProductsViewModel> logger)
    {
        this.service = service; this.session = session; this.logger = logger;
        session.Changed += (_, _) =>
        {
            Products.Clear(); Categories.Clear(); Units.Clear(); Brands.Clear(); SelectedProduct = null; Total = 0; Page = 1;
            ResetEditor(); StatusMessage = "Busca o registra tu primer producto.";
            OnPropertyChanged(nameof(CanView)); OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanManage));
        };
    }
    partial void OnTracksExpiryChanged(bool value) { if (value) TracksLot = true; }
    partial void OnTracksLotChanged(bool value) { if (!value) TracksExpiry = false; }

    [RelayCommand] private async Task SearchAsync() { Page = 1; await LoadAsync(); }
    [RelayCommand] private async Task ReloadAsync() => await LoadAsync();
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await LoadAsync(); }

    private async Task LoadAsync()
    {
        if (IsBusy || !service.CanRead) return;
        IsBusy = true; var revision = session.Revision;
        try
        {
            var catalogs = await service.GetCatalogsAsync();
            if (catalogs.IsSuccess && catalogs.Value is { } values && revision == session.Revision) SetCatalogs(values);
            var result = await service.SearchAsync(new(Search.Trim(), IncludeInactive, Page));
            if (revision != session.Revision) return;
            if (!result.IsSuccess || result.Value is null) { StatusMessage = result.Message ?? "No se pudo consultar."; return; }
            Products.Clear(); foreach (var row in result.Value.Items) Products.Add(row);
            Total = result.Value.Total; SelectedProduct = null;
            StatusMessage = Total == 0 ? "No hay productos con estos filtros. Puedes registrar uno nuevo." : "Catálogo actualizado. El stock se consulta; no se edita desde esta pantalla.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void NewProduct() { if (CanEdit) ResetEditor(); }
    [RelayCommand] private void EditSelected()
    {
        if (!CanEdit || SelectedProduct is null) return;
        editing = SelectedProduct;
        Sku = editing.Sku; Name = editing.Name; Description = editing.Description ?? "";
        Price = editing.Price.ToString("0.00", CultureInfo.InvariantCulture);
        MinimumStock = editing.MinimumStock.ToString(CultureInfo.InvariantCulture);
        MaximumStock = editing.MaximumStock?.ToString(CultureInfo.InvariantCulture) ?? "";
        Category = Categories.FirstOrDefault(c => c.Id == editing.CategoryId); Brand = Brands.FirstOrDefault(b => b.Id == editing.BrandId);
        Unit = Units.FirstOrDefault(u => u.Id == editing.UnitId); TracksLot = editing.TracksLot; TracksExpiry = editing.TracksExpiry; Active = editing.Active;
        EditorTitle = "Editar producto";
    }
    private void ResetEditor()
    {
        editing = new(); Sku = ""; Name = ""; Description = ""; Price = "0.00"; MinimumStock = "0"; MaximumStock = "";
        Category = Categories.FirstOrDefault(); Brand = Brands.FirstOrDefault(); Unit = Units.FirstOrDefault();
        TracksExpiry = false; TracksLot = false; Active = true; EditorTitle = "Nuevo producto"; NewCategory = "";
    }

    [RelayCommand] private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (Brand is null && editing.BrandId is not null) { StatusMessage = "La marca anterior no está disponible. Selecciona una marca o Sin marca."; return; }
        if (!TryNumber(Price, out var parsedPrice) || !TryNumber(MinimumStock, out var minimum) ||
            (!string.IsNullOrWhiteSpace(MaximumStock) && !TryNumber(MaximumStock, out _)))
        { StatusMessage = "Usa valores numéricos con punto decimal; por ejemplo, 12.50."; return; }
        var product = new ProductData
        {
            Id = editing.Id, RowVersion = editing.RowVersion.ToArray(), Sku = Sku.Trim(), Name = Name.Trim(), Description = Description.Trim(),
            CategoryId = Category?.Id ?? 0, BrandId = Brand?.Id is > 0 ? Brand.Id : null, UnitId = checked((short)(Unit?.Id ?? 0)),
            Price = parsedPrice, MinimumStock = minimum, MaximumStock = string.IsNullOrWhiteSpace(MaximumStock) ? null : decimal.Parse(MaximumStock, CultureInfo.InvariantCulture),
            TracksLot = TracksLot, TracksExpiry = TracksExpiry, TracksSerial = editing.TracksSerial, Active = Active
        };
        IsBusy = true; var revision = session.Revision;
        try
        {
            var result = await service.SaveAsync(product);
            if (revision != session.Revision) return;
            StatusMessage = result.IsSuccess ? "Producto guardado." : result.Message ?? "No se pudo guardar.";
            if (!result.IsSuccess) return;
            ResetEditor(); IsBusy = false; await LoadAsync(); StatusMessage = "Producto guardado correctamente.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task CreateCategoryAsync()
    {
        if (!CanEdit) return;
        IsBusy = true; var revision = session.Revision;
        try
        {
            var result = await service.CreateCategoryAsync(NewCategory);
            if (revision != session.Revision) return;
            if (!result.IsSuccess) { StatusMessage = result.Message ?? "No se pudo crear la categoría."; return; }
            var catalogs = await service.GetCatalogsAsync();
            if (revision != session.Revision) return;
            if (catalogs.Value is { } values) SetCatalogs(values);
            Category = Categories.FirstOrDefault(c => c.Id == result.Value); NewCategory = ""; StatusMessage = "Categoría creada y seleccionada.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }
    private void SetCatalogs(ProductCatalogs values)
    {
        var categoryId = Category?.Id; var brandId = Brand?.Id; var unitId = Unit?.Id;
        Categories.Clear(); foreach (var v in values.Categories) Categories.Add(v);
        Brands.Clear(); Brands.Add(new() { Id = 0, Name = "Sin marca" }); foreach (var v in values.Brands) Brands.Add(v);
        Units.Clear(); foreach (var v in values.Units) Units.Add(v);
        Category = categoryId is null ? Categories.FirstOrDefault() : Categories.FirstOrDefault(c => c.Id == categoryId);
        Brand = brandId is null ? Brands.FirstOrDefault() : Brands.FirstOrDefault(c => c.Id == brandId);
        Unit = unitId is null ? Units.FirstOrDefault() : Units.FirstOrDefault(c => c.Id == unitId);
    }
    private static bool TryNumber(string value, out decimal number) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
    private void Fail(Exception ex)
    {
        logger.LogWarning("Falló el módulo de productos: {ErrorType}.", ex.GetType().Name);
        if (ex is UnauthorizedAccessException) { session.Clear(); return; }
        StatusMessage = "No se pudo completar la operación. Revisa la conexión e inténtalo de nuevo. Si estabas guardando, recarga antes de repetir.";
    }
}
