using GoDental.Application.DTOs;
using GoDental.Application.Online;
namespace GoDental.Application.Services;
public sealed partial class OnlineService
{
    public async Task<OnlineDiscoveryResult> DiscoverAsync(string department,CancellationToken ct=default)
    {
        Read("COMERCIAL.VER","CLIENTES.VER","COMERCIAL.PROSPECTAR");
        var revision=CurrentSession is GoDental.Application.Abstractions.Security.ISessionController s?s.Revision:0;
        var area=OnlineMapCatalog.Departments.SingleOrDefault(d=>d.Name==department)??throw new ArgumentException("Selecciona un departamento del mapa.");
        if(discovery is null)throw new ArgumentException("El proveedor de búsqueda no está configurado.");
        await repository.AuthorizeOnlineDiscoveryAsync(UserId,ct);
        if(!CanManage||CurrentSession is GoDental.Application.Abstractions.Security.ISessionController c&&revision!=c.Revision)throw new UnauthorizedAccessException("La sesión cambió.");
        var result=await discovery.DiscoverAsync(area,ct);
        await repository.AuthorizeOnlineDiscoveryAsync(UserId,ct);
        if(!CanManage||CurrentSession is GoDental.Application.Abstractions.Security.ISessionController c2&&revision!=c2.Revision)throw new UnauthorizedAccessException("La sesión cambió.");
        return result;
    }
}
