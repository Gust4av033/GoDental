using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Configuration;
public interface IPrintPreferencesStore
{
    Task<PrintPreferences> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(PrintPreferences preferences, CancellationToken ct = default);
}
