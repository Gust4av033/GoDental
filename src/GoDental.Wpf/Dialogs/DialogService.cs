using System.Windows;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.Views.Inventory;
using GoDental.Wpf.Views.Auth;

namespace GoDental.Wpf.Dialogs;

public sealed class DialogService(System.Windows.Application application) : IDialogService
{
    public Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor)
    {
        var window = new DialogWindow(editor, new ProductEditorView { DataContext = editor });
        return Show(window);
    }

    public async Task<bool> ShowCategoryManagerAsync(CategoriesViewModel manager)
    {
        await manager.ReloadAsync();
        using var model = new CategoryDialogModel(manager);
        return await Show(new DialogWindow(model, new CategoryDialogView { DataContext = manager }));
    }

    public Task<bool> ShowCreateUserAsync(UsersViewModel manager)
    {
        var view = new UserCreateDialogView { DataContext = manager };
        using var model = new UserDialogModel(manager, true, view.ReadPasswords, view.ClearPasswords, view.HasPasswords);
        return Show(new DialogWindow(model, view));
    }

    public Task<bool> ShowManageUserAsync(UsersViewModel manager)
    {
        var view = new UserManageDialogView { DataContext = manager };
        using var model = new UserDialogModel(manager, false, view.ReadPasswords, view.ClearPasswords, view.HasPasswords);
        return Show(new DialogWindow(model, view));
    }

    public Task<bool> ShowInventoryMovementAsync(InventoryViewModel manager)
    {
        using var model = new InventoryMovementDialogModel(manager);
        return Show(new DialogWindow(model, new InventoryMovementDialogView { DataContext = manager }));
    }

    private Task<bool> Show(DialogWindow window)
    {
        var owner = application.MainWindow;
        if (owner is { IsVisible: true }) window.Owner = owner;
        return Task.FromResult(window.ShowDialog() == true);
    }
}
