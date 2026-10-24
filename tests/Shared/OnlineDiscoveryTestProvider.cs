using GoDental.Application.Abstractions.Online;
using GoDental.Application.DTOs;
using GoDental.Application.Online;
internal sealed class OnlineDiscoveryTestProvider:IOnlineDiscoveryProvider
{
    public Task<OnlineDiscoveryResult>? Pending {get;set;}
    public int Calls {get;private set;}
    public static OnlineDiscoveryResult Data=>new([new("node/123","Negocio sintético de mapa","https://www.openstreetmap.org/node/123",-89.19,13.7,"+503 70000000",null,"mapa@example.com","https://negocio.example.com/","Dirección publicada de ejemplo")],DateTimeOffset.UtcNow,false,"OpenStreetMap · datos sintéticos de prueba");
    public OnlineDiscoveryServer LastServer {get;private set;}
    public Task<OnlineDiscoveryResult> DiscoverAsync(OnlineMapDepartment d,CancellationToken ct,OnlineDiscoveryServer server=OnlineDiscoveryServer.Main){Calls++;LastServer=server;return Pending??Task.FromResult(Data);}
}
