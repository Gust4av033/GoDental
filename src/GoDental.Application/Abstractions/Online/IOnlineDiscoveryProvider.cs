using GoDental.Application.DTOs;
using GoDental.Application.Online;
namespace GoDental.Application.Abstractions.Online;
public interface IOnlineDiscoveryProvider
{
    Task<OnlineDiscoveryResult> DiscoverAsync(OnlineMapDepartment department,CancellationToken ct);
}
