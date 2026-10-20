using GoDental.Application.DTOs;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public sealed class CountStartDialogModel(IReadOnlyList<CatalogOption> warehouses, ProductPickerViewModel picker,
    Func<OperationDialogModel, Task<bool>> save) : OperationDialogModel(save)
{
    public override string Title => "Abrir conteo físico";
    public IReadOnlyList<CatalogOption> Warehouses { get; } = warehouses;
    public CatalogOption? Warehouse { get; set; } = warehouses.FirstOrDefault();
    public ProductPickerViewModel Picker { get; } = picker;
}
