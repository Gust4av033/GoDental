using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class BarcodesViewModel : MvpWorkspaceViewModel
{
    private readonly InventorySetupService service;
    private readonly IMvpDialogService dialogs;
    private int productId;
    private string productLabel = "";
    private byte[]? version;
    public ObservableCollection<BarcodeData> Items { get; } = [];
    public IReadOnlyList<string> Types { get; } = ["CODE128", "EAN13", "UPC", "OTRO"];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanToggle))] private BarcodeData? selected;
    [ObservableProperty] private bool isEmpty = true;
    public override bool CanRead => service.CanReadBarcodes;
    public bool CanManage => CanEdit && service.CanManageBarcodes && version is not null;
    public bool CanToggle => CanManage && Selected is not null;
    protected override void PermissionsChanged() { OnPropertyChanged(nameof(CanManage)); OnPropertyChanged(nameof(CanToggle)); }
    protected override void Reset() { productId = 0; productLabel = ""; version = null; Items.Clear(); Selected = null; IsEmpty = true; }
    public BarcodesViewModel(InventorySetupService service, IMvpDialogService dialogs, ISessionController session,
        MvpOperationService operations, ILogger<BarcodesViewModel> logger) : base(session, operations, logger)
    { this.service = service; this.dialogs = dialogs; }
    public async Task OpenAsync(ProductData product)
    {
        if (IsBusy || IsEditing || !Operation.CanStart) return;
        productId = product.Id; productLabel = $"{product.Name} · {product.Sku}";
        await ReloadAsync();
    }
    [RelayCommand] private Task ReloadAsync() => ReadAsync(async revision =>
    {
        if (productId <= 0) return;
        version = null; PermissionsChanged(); Items.Clear(); Selected = null; IsEmpty = true;
        var result = await service.BarcodesAsync(productId);
        if (!Current(revision)) return;
        version = result.ProductVersion.ToArray();
        foreach (var item in result.Items) Items.Add(item);
        IsEmpty = Items.Count == 0; PermissionsChanged();
        StatusMessage = IsEmpty ? "Este producto no tiene códigos. Puedes registrar uno externo o generar un código interno." : "Los códigos activos permiten buscar este producto en el POS. Desactivar conserva el historial.";
    });
    [RelayCommand] private Task AddAsync() => EditAsync(null);
    [RelayCommand] private Task ToggleAsync() => Selected is { } item ? EditAsync(item) : Task.CompletedTask;
    private async Task EditAsync(BarcodeData? existing)
    {
        if (!CanManage || version is null) return;
        var expected = version.ToArray(); var target = productId;
        using var form = new MvpFormModel(existing is null ? "Agregar código de barras" : existing.Active ? "Desactivar código" : "Activar código", "Guardar código", Operation, async f =>
        {
            var id = Guid.NewGuid();
            var request = new BarcodeChange(id, target, expected, existing?.Code ?? f.Text("code"), existing?.Type ?? f.Choice<string>("type"), existing is null || !existing.Active, f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.SaveBarcodeAsync(r), _ => ReloadAsync());
        }) { Description = existing is null ? $"{productLabel}. Deja el código vacío y elige CODE128 para generar el interno. Para EAN13/UPC incluye el dígito de control. No se imprime una etiqueta en este paso."
            : $"{productLabel} · {existing.Code} · {existing.Type}. {(existing.Active ? "Dejará de encontrarse por este código en el POS." : "Volverá a encontrarse por este código en el POS.")} No se elimina ni se reasigna a otro producto." };
        if (existing is null) form.Input("code", "Código externo (vacío para generar interno)").Select("type", "Tipo", Types, "CODE128", display: "");
        form.Input("reason", "Motivo");
        IsEditing = true;
        try { await dialogs.ShowAsync(form); }
        finally { IsEditing = false; }
    }
}
