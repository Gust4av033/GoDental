namespace GoDental.Wpf.Dialogs;
public interface ICustomerDialogService
{
    Task<bool> ShowCustomerEditorAsync(CustomerEditorDialogModel model);
}
