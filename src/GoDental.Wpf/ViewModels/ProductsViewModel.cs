using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ProductsViewModel : ObservableObject
{
    private readonly ProductService service;
    private readonly ISessionController session;
    private readonly ILogger<ProductsViewModel> logger;
    private readonly IDialogService dialogs;
    private readonly ProductEditorFactory editorFactory;
    private ProductCatalogs? catalogs;
    private bool dialogOpen;
    public CategoriesViewModel? CategoryManager { get; }
    public InventoryViewModel? Inventory { get; }
    public ObservableCollection<ProductData> Products { get; } = [];
    public ObservableCollection<CatalogOption> Categories { get; } = [];
    public ObservableCollection<CatalogOption> Brands { get; } = [];
    [ObservableProperty] private CatalogOption? filterCategory;
    [ObservableProperty] private CatalogOption? filterBrand;
    [ObservableProperty] private bool lowStockOnly;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool includeInactive;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEditSelected))] [NotifyPropertyChangedFor(nameof(CanOpenInventory))] private ProductData? selectedProduct;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotBusy))] [NotifyPropertyChangedFor(nameof(CanEdit))] [NotifyPropertyChangedFor(nameof(CanEditSelected))] [NotifyPropertyChangedFor(nameof(CanOpenInventory))] [NotifyPropertyChangedFor(nameof(ShowEmptyState))] [NotifyPropertyChangedFor(nameof(ShowErrorState))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Busca o registra tu primer producto.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowEmptyState))] [NotifyPropertyChangedFor(nameof(ShowErrorState))] private bool hasLoadError;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(PageLabel))] [NotifyPropertyChangedFor(nameof(IsEmpty))] [NotifyPropertyChangedFor(nameof(ShowEmptyState))] [NotifyPropertyChangedFor(nameof(ShowErrorState))] [NotifyPropertyChangedFor(nameof(ShowCatalogTable))] private int total;
    public bool CanView => service.CanRead;
    public bool CanManage => service.CanManage;
    public bool CanEdit => CanManage && IsNotBusy && !dialogOpen;
    public bool CanEditSelected => CanEdit && SelectedProduct is not null;
    public bool CanOpenInventory => IsNotBusy && SelectedProduct is not null && Inventory is not null;
    public bool IsNotBusy => !IsBusy && Inventory?.IsBusy != true && CategoryManager?.IsBusy != true;
    public bool IsEmpty => Total == 0;
    public bool ShowEmptyState => IsEmpty && !IsBusy && !HasLoadError;
    public bool ShowErrorState => IsEmpty && !IsBusy && HasLoadError;
    public bool ShowCatalogTable => Total > 0;
    public string PageLabel => $"{Total} productos · Página {Page} de {Math.Max(1, (int)Math.Ceiling(Total / 30d))}";

    public ProductsViewModel(ProductService service, ISessionController session, ILogger<ProductsViewModel> logger,
        IDialogService dialogs, ProductEditorFactory editorFactory, InventoryViewModel? inventory = null, CategoriesViewModel? categories = null)
    {
        this.service = service; this.session = session; this.logger = logger;
        this.dialogs = dialogs; this.editorFactory = editorFactory;
        Inventory = inventory; CategoryManager = categories;
        if (inventory is not null) inventory.InventoryChanged += (_, _) => ReloadCommand.Execute(null);
        if (categories is not null) categories.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected)); };
        if (inventory is not null) inventory.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(IsNotBusy)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected)); OnPropertyChanged(nameof(CanOpenInventory)); };
        session.Changed += (_, _) =>
        {
            Products.Clear(); Categories.Clear(); Brands.Clear(); catalogs = null; SelectedProduct = null; Total = 0; Page = 1; HasLoadError = false;
            StatusMessage = "Busca o registra tu primer producto.";
            OnPropertyChanged(nameof(CanView)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected)); OnPropertyChanged(nameof(CanOpenInventory)); OnPropertyChanged(nameof(CanManage));
        };
    }

    [RelayCommand] private async Task ClearFiltersAsync() { Search = ""; FilterCategory = null; FilterBrand = null; LowStockOnly = false; IncludeInactive = false; Page = 1; await LoadAsync(); }
    [RelayCommand] private async Task SearchAsync() { Page = 1; await LoadAsync(); }
    [RelayCommand] private async Task ReloadAsync() => await LoadAsync();
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await LoadAsync(); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await LoadAsync(); }

    private async Task<bool> LoadAsync()
    {
        if (!IsNotBusy || !service.CanRead) return false;
        IsBusy = true; HasLoadError = false; var revision = session.Revision;
        try
        {
            var fetched = await service.GetCatalogsAsync();
            if (fetched.IsSuccess && fetched.Value is { } values && revision == session.Revision)
            { catalogs = values; SetCatalogs(values); }
            var result = await service.SearchAsync(new(Search.Trim(), IncludeInactive, Page, CategoryId: FilterCategory?.Id, BrandId: FilterBrand?.Id, LowStockOnly: LowStockOnly));
            if (revision != session.Revision) return false;
            if (!result.IsSuccess || result.Value is null) { HasLoadError = true; StatusMessage = result.Message ?? "No se pudo consultar."; return false; }
            Products.Clear(); foreach (var row in result.Value.Items) Products.Add(row);
            Total = result.Value.Total; SelectedProduct = null;
            StatusMessage = Total == 0 ? "No hay productos con estos filtros. Puedes registrar uno nuevo." : "Catálogo actualizado. El stock se consulta; no se edita desde esta pantalla.";
            return true;
        }
        catch (Exception ex) { HasLoadError = true; Fail(ex); return false; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task OpenInventoryAsync()
    {
        if (!IsNotBusy || SelectedProduct is null || Inventory is null) return;
        await Inventory.OpenAsync(SelectedProduct);
    }

    [RelayCommand] private async Task NewProductAsync() => await OpenEditorAsync(null);
    [RelayCommand] private async Task ManageCategoriesAsync()
    {
        if (!CanEdit || CategoryManager is null) return;
        dialogOpen = true; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected));
        try
        {
            if (await dialogs.ShowCategoryManagerAsync(CategoryManager) && await LoadAsync())
                StatusMessage = "Categoría actualizada. Catálogo renovado.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { dialogOpen = false; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected)); }
    }
    [RelayCommand] private async Task EditSelectedAsync()
    {
        if (SelectedProduct is not null) await OpenEditorAsync(SelectedProduct);
    }

    private async Task OpenEditorAsync(ProductData? product)
    {
        if (!CanEdit) return;
        if (catalogs is null) { StatusMessage = "Actualiza el catálogo antes de abrir el formulario."; return; }
        dialogOpen = true; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected));
        try
        {
            using var editor = editorFactory.Create(product, catalogs);
            var saved = await dialogs.ShowProductEditorAsync(editor);
            if ((saved || editor.CatalogChanged) && await LoadAsync())
                StatusMessage = saved ? "Producto guardado correctamente." : "Categoría creada. Producto sin guardar.";
        }
        catch (Exception ex) { Fail(ex); }
        finally { dialogOpen = false; OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanEditSelected)); }
    }

    private void SetCatalogs(ProductCatalogs values)
    {
        var filterCategoryId = FilterCategory?.Id; var filterBrandId = FilterBrand?.Id;
        Categories.Clear(); foreach (var value in values.Categories) Categories.Add(value);
        Brands.Clear(); Brands.Add(new() { Id = 0, Name = "Sin marca" }); foreach (var value in values.Brands) Brands.Add(value);
        FilterCategory = Categories.FirstOrDefault(c => c.Id == filterCategoryId);
        FilterBrand = Brands.FirstOrDefault(b => b.Id == filterBrandId);
    }

    private void Fail(Exception ex)
    {
        logger.LogWarning("Falló el módulo de productos: {ErrorType}.", ex.GetType().Name);
        if (ex is UnauthorizedAccessException) { session.Clear(); return; }
        StatusMessage = "No se pudo completar la operación. Revisa la conexión e inténtalo de nuevo. Si estabas guardando, recarga antes de repetir.";
    }
}
