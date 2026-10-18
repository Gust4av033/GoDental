using System.Windows;
using GoDental.Wpf.ViewModels;
using GoDental.Wpf.Views.Inventory;

namespace GoDental.Wpf.Dialogs;

public sealed class DialogService(System.Windows.Application application) : IDialogService
{
    public Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor)
    {
        var window = new DialogWindow(editor, new ProductEditorView { DataContext = editor });
        var owner = application.MainWindow;
        if (owner is { IsVisible: true }) window.Owner = owner;
        return Task.FromResult(window.ShowDialog() == true);
    }
}
