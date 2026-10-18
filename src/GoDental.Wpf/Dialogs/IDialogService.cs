using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Dialogs;

public interface IDialogService
{
    Task<bool> ShowProductEditorAsync(ProductEditorViewModel editor);
}
