using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Configuration;

public interface IConnectionHealthCheck
{
    Task<ConnectionCheckResult> CheckAsync(WorkstationSettings settings, CancellationToken cancellationToken = default);
}
