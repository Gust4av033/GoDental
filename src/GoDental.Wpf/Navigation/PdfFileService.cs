using System.IO;
using Microsoft.Win32;
using GoDental.Wpf.ViewModels.Operations;

namespace GoDental.Wpf.Navigation;

public sealed class PdfFileService : IPdfFileService
{
    public async Task<bool> SaveAsync(byte[] pdf, string suggestedName)
    {
        var dialog = new SaveFileDialog { Title = "Guardar comprobante interno", Filter = "Documento PDF (*.pdf)|*.pdf", DefaultExt = ".pdf", AddExtension = true, FileName = suggestedName, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return false;
        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        return true;
    }
}
