using System.Text.Json;
using GoDental.Application.Abstractions.Configuration;
using GoDental.Application.DTOs;
namespace GoDental.Infrastructure.Data;

public sealed class LocalPrintPreferencesStore(string path) : IPrintPreferencesStore
{
    public async Task<PrintPreferences> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(path)) return new();
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<PrintPreferences>(stream, cancellationToken: ct)
            ?? throw new ArgumentException("No se pudo leer el perfil local de impresión.");
        value.Receipt?.Validate(narrow: value.Receipt.WidthMm < 140); value.Labels?.Validate();
        return value;
    }
    public async Task SaveAsync(PrintPreferences value, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(value), ct);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
