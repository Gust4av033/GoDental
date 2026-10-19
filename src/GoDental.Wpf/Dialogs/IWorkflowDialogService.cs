namespace GoDental.Wpf.Dialogs;

public interface IWorkflowDialogService
{
    Task<bool> ShowCountStartAsync(CountStartDialogModel model);
    Task<bool> ShowSupplierEditorAsync(SupplierEditorDialogModel model);
}
