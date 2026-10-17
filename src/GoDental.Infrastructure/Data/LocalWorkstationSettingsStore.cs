using System.Text.Json;
using GoDental.Application.DTOs;
using GoDental.Application.Abstractions.Configuration;

namespace GoDental.Infrastructure.Data;

public sealed class LocalWorkstationSettingsStore(string path) : IWorkstationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<WorkstationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) return new();
        await using var stream = File.OpenRead(path);
        var settings = await JsonSerializer.DeserializeAsync<WorkstationSettings>(stream, JsonOptions, cancellationToken);
        return settings is null
            ? throw new JsonException("El perfil de estación no es válido.")
            : new(settings.Server ?? "", settings.Database ?? "", settings.Theme == "Dark" ? "Dark" : "Light", settings.LocalDevelopment);
    }

    public async Task SaveAsync(WorkstationSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
