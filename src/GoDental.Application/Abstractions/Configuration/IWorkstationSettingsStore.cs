using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Configuration;

public interface IWorkstationSettingsStore
{
    Task<WorkstationSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorkstationSettings settings, CancellationToken cancellationToken = default);
}
