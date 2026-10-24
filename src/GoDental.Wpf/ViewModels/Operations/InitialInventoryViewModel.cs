using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed class InitialInventoryViewModel : MvpWorkspaceViewModel
{
    private readonly InventorySetupService setup;
    private readonly IMvpDialogService dialogs;
    public override bool CanRead => Has("INVENTARIO.VER");
    public bool CanInitialize => CanEdit && Has("INVENTARIO.AJUSTAR");
    protected override void Reset() { }
    protected override void PermissionsChanged() => OnPropertyChanged(nameof(CanInitialize));
    public InitialInventoryViewModel(InventorySetupService setup, IMvpDialogService dialogs, ISessionController session,
        MvpOperationService operations, ILogger<InitialInventoryViewModel> logger) : base(session, operations, logger)
    { this.setup = setup; this.dialogs = dialogs; }

    public async Task OpenAsync(ProductData product, CatalogOption warehouse, Func<Task> refresh)
    {
        if (!CanInitialize || !product.Active) return;
        using var form = new MvpFormModel("Existencias iniciales", "Registrar saldo", Operation, async f =>
        {
            if (product.TracksLot && string.IsNullOrWhiteSpace(f.Text("lot"))) throw new ArgumentException("Escribe el número del lote inicial.");
            if (product.TracksExpiry && f.Date("expires") is null) throw new ArgumentException("Selecciona la fecha de vencimiento.");
            var id = Guid.NewGuid();
            var request = new InitialInventory(id, product.Id, warehouse.Id, f.Number("quantity"), f.Number("cost"), f.Text("reason"),
                product.TracksLot ? f.Text("lot") : null, product.TracksLot ? f.Date("expires") : null, product.TracksLot ? f.Date("manufactured") : null);
            await Operation.StartAsync(request, id, q => setup.InitializeAsync(q), _ => refresh());
        })
        {
            Description = $"{product.Name} · {product.Sku} · {warehouse.Name}. Sólo para un producto/almacén sin saldo ni historial. Registra cantidad física y costo unitario USD (hasta seis decimales). El servicio valida y genera Kardex y auditoría."
        };
        form.Input("quantity", "Cantidad inicial").Input("cost", "Costo unitario USD");
        if (product.TracksLot) form.Input("lot", "Número de lote").Calendar("manufactured", "Fecha de fabricación (opcional)").Calendar("expires", "Fecha de vencimiento");
        form.Input("reason", "Motivo de la carga inicial");
        IsEditing = true;
        try { await dialogs.ShowAsync(form); }
        finally { IsEditing = false; }
    }
}
