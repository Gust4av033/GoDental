using System.IO;
using Microsoft.Win32;
using GoDental.Wpf.ViewModels.Operations;
using GoDental.Application.Abstractions.Security;

namespace GoDental.Wpf.Navigation;

public sealed class PdfFileService(ISessionController session) : IPdfFileService
{
    public async Task<bool> SaveAsync(byte[] pdf, string suggestedName)
    {
        var revision = session.Revision;
        var dialog = new SaveFileDialog { Title = "Guardar comprobante interno", Filter = "Documento PDF (*.pdf)|*.pdf", DefaultExt = ".pdf", AddExtension = true, FileName = suggestedName, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return false;
        if (session.Revision != revision || !session.IsAuthenticated || !session.Permissions.Contains("VENTAS.VER") || !session.Permissions.Contains("VENTAS.REIMPRIMIR")) throw new UnauthorizedAccessException("La sesión cambió antes de guardar el comprobante.");
        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        return true;
    }
}
