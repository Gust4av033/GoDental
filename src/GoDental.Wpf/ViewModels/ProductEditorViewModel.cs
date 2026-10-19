using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class ProductEditorViewModel : ObservableObject, IDialogModel, IDisposable
{
    private readonly ProductService service;
    private readonly ISessionController session;
    private readonly ILogger<ProductEditorViewModel> logger;
    private readonly ProductData editing;
    private string initialState = "";
    public event EventHandler? Saved;
    public event EventHandler? CancelRequested;
    public ObservableCollection<CatalogOption> Categories { get; } = [];
    public ObservableCollection<CatalogOption> Brands { get; } = [];
    public ObservableCollection<CatalogOption> Units { get; } = [];
    [ObservableProperty] private string sku = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string price = "0.00";
    [ObservableProperty] private string minimumStock = "0";
    [ObservableProperty] private string maximumStock = "";
    [ObservableProperty] private string expiryAlertDays = "90";
    [ObservableProperty] private CatalogOption? category;
    [ObservableProperty] private CatalogOption? brand;
    [ObservableProperty] private CatalogOption? unit;
    [ObservableProperty] private bool tracksLot;
    [ObservableProperty] private bool tracksExpiry;
    [ObservableProperty] private bool active = true;
    [ObservableProperty] private string newCategory = "";
    [ObservableProperty] private string statusMessage = "Completa los datos y guarda el producto.";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEdit))] private bool isBusy;
    public bool CanEdit => service.CanManage && !IsBusy;
    public bool CatalogChanged { get; private set; }
    ICommand IDialogModel.SaveCommand => SaveCommand;
    ICommand IDialogModel.CancelCommand => CancelCommand;
    public bool HasUnsavedChanges => Snapshot() != initialState;
    public string Title => editing.Id == 0 ? "Nuevo producto" : "Editar producto";
    public string PrimaryActionLabel => "Guardar";
    public string SecondaryActionLabel => "Cancelar";
    public bool ShowPrimaryAction => true;

    public ProductEditorViewModel(ProductService service, ISessionController session, ILogger<ProductEditorViewModel> logger, ProductData? product, ProductCatalogs catalogs)
    {
        this.service = service; this.session = session; this.logger = logger;
        session.Changed += OnSessionChanged;
        editing = product ?? new ProductData();
        SetCatalogs(catalogs);
        if (product is not null)
        {
            ExpiryAlertDays = editing.ExpiryAlertDays?.ToString() ?? "90";
            Sku = editing.Sku; Name = editing.Name; Description = editing.Description ?? "";
            Price = editing.Price.ToString("0.00", CultureInfo.InvariantCulture);
            MinimumStock = editing.MinimumStock.ToString(CultureInfo.InvariantCulture);
            MaximumStock = editing.MaximumStock?.ToString(CultureInfo.InvariantCulture) ?? "";
            Category = Categories.FirstOrDefault(c => c.Id == editing.CategoryId);
            Brand = Brands.FirstOrDefault(b => b.Id == editing.BrandId);
            Unit = Units.FirstOrDefault(u => u.Id == editing.UnitId);
            TracksLot = editing.TracksLot; TracksExpiry = editing.TracksExpiry; Active = editing.Active;
        }
        initialState = Snapshot();
    }

    partial void OnTracksExpiryChanged(bool value) { if (value) TracksLot = true; }
    partial void OnTracksLotChanged(bool value) { if (!value) TracksExpiry = false; }

    private void OnSessionChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(CanEdit));
    public void Dispose() => session.Changed -= OnSessionChanged;

    [RelayCommand] private void Cancel() => CancelRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand] private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (Brand is null && editing.BrandId is not null) { StatusMessage = "La marca anterior no está disponible. Selecciona una marca o Sin marca."; return; }
        if (!TryNumber(Price, out var parsedPrice) || !TryNumber(MinimumStock, out var minimum) ||
            (!string.IsNullOrWhiteSpace(MaximumStock) && !TryNumber(MaximumStock, out _)))
        { StatusMessage = "Usa valores numéricos con punto decimal; por ejemplo, 12.50."; return; }
        if (!short.TryParse(ExpiryAlertDays, out var alertDays) || alertDays < 0) { StatusMessage = "Indica días de alerta entre 0 y 32767."; return; }
        var product = new ProductData
        {
            Id = editing.Id, RowVersion = editing.RowVersion.ToArray(), Sku = Sku.Trim(), Name = Name.Trim(), Description = Description.Trim(),
            CategoryId = Category?.Id ?? 0, BrandId = Brand?.Id is > 0 ? Brand.Id : null, UnitId = checked((short)(Unit?.Id ?? 0)),
            Price = parsedPrice, MinimumStock = minimum, MaximumStock = string.IsNullOrWhiteSpace(MaximumStock) ? null : decimal.Parse(MaximumStock, CultureInfo.InvariantCulture),
            ExpiryAlertDays = alertDays, TracksLot = TracksLot, TracksExpiry = TracksExpiry, TracksSerial = editing.TracksSerial, Active = Active
        };
        IsBusy = true; var revision = session.Revision;
        try
        {
            var result = await service.SaveAsync(product);
            if (revision != session.Revision) return;
            StatusMessage = result.IsSuccess ? "Producto guardado." : result.Message ?? "No se pudo guardar.";
            if (result.IsSuccess) Saved?.Invoke(this, EventArgs.Empty);
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
            Category = Categories.FirstOrDefault(c => c.Id == result.Value);
            NewCategory = ""; StatusMessage = "Categoría creada y seleccionada.";
            CatalogChanged = true;
        }
        catch (Exception ex) { Fail(ex); }
        finally { IsBusy = false; }
    }

    private void SetCatalogs(ProductCatalogs values)
    {
        var categoryId = Category?.Id; var brandId = Brand?.Id; var unitId = Unit?.Id;
        Categories.Clear(); foreach (var value in values.Categories) Categories.Add(value);
        Brands.Clear(); Brands.Add(new() { Id = 0, Name = "Sin marca" }); foreach (var value in values.Brands) Brands.Add(value);
        Units.Clear(); foreach (var value in values.Units) Units.Add(value);
        Category = categoryId is null ? Categories.FirstOrDefault() : Categories.FirstOrDefault(c => c.Id == categoryId);
        Brand = brandId is null ? Brands.FirstOrDefault() : Brands.FirstOrDefault(c => c.Id == brandId);
        Unit = unitId is null ? Units.FirstOrDefault() : Units.FirstOrDefault(c => c.Id == unitId);
    }

    private string Snapshot() => string.Join("\u001f", Sku, Name, Description, Price, MinimumStock, MaximumStock, ExpiryAlertDays,
        Category?.Id.ToString(), Brand?.Id.ToString(), Unit?.Id.ToString(), TracksLot, TracksExpiry, Active, NewCategory);
    private static bool TryNumber(string value, out decimal number) => decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
    private void Fail(Exception ex)
    {
        logger.LogWarning("Falló el editor de producto: {ErrorType}.", ex.GetType().Name);
        if (ex is UnauthorizedAccessException) { session.Clear(); return; }
        StatusMessage = "No se pudo completar la operación. Revisa la conexión e inténtalo de nuevo. Si estabas guardando, recarga antes de repetir.";
    }
}
