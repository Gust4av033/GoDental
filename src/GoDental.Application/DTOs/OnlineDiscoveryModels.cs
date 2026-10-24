namespace GoDental.Application.DTOs;
public enum OnlineDiscoveryServer { Main, Alternative }
public sealed record OnlineDiscoveredBusiness(string SourceId,string Name,string SourceUrl,double Longitude,double Latitude,string? Phone,string? WhatsApp,string? Email,string? Website,string? Address);
public sealed record OnlineDiscoveryResult(IReadOnlyList<OnlineDiscoveredBusiness> Items,DateTimeOffset Obtained,bool Limited,string Attribution);
