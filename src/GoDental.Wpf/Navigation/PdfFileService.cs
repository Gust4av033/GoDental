using System.IO;
using Microsoft.Win32;
using GoDental.Wpf.ViewModels.Operations;
using GoDental.Application.Abstractions.Security;

namespace GoDental.Wpf.Navigation;

public sealed class PdfFileService(ISessionController session) : IPdfFileService
{
    public Task<bool> SaveAsync(byte[] pdf, string suggestedName) => SaveCoreAsync(pdf, suggestedName, false);
    public Task<bool> SaveLabelsAsync(byte[] pdf, string suggestedName) => SaveCoreAsync(pdf, suggestedName, true);
    private async Task<bool> SaveCoreAsync(byte[] pdf, string suggestedName, bool labels)
    {
        var revision = session.Revision;
        var dialog = new SaveFileDialog { Title = labels ? "Guardar etiquetas" : "Guardar comprobante interno", Filter = "Documento PDF (*.pdf)|*.pdf", DefaultExt = ".pdf", AddExtension = true, FileName = suggestedName, OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return false;
        if (session.Revision != revision || !session.IsAuthenticated || !(labels ? session.Permissions.Contains("INVENTARIO.VER") : session.Permissions.Contains("VENTAS.VER") && session.Permissions.Contains("VENTAS.REIMPRIMIR"))) throw new UnauthorizedAccessException("La sesión cambió antes de guardar el comprobante.");
        await File.WriteAllBytesAsync(dialog.FileName, pdf);
        return true;
    }
}
