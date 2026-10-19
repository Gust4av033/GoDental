using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public interface IDialogService
{
    Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor);
    Task<bool> ShowCategoryManagerAsync(CategoriesViewModel manager);
    Task<bool> ShowCreateUserAsync(UsersViewModel manager);
    Task<bool> ShowManageUserAsync(UsersViewModel manager);
    Task<bool> ShowInventoryMovementAsync(InventoryViewModel manager);
}
