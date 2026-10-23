using GoDental.Application.Abstractions.Online;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed partial class OnlineService(IOnlineRepository repository,IUserSession session,IOnlineDiscoveryProvider? discovery=null):MvpServiceAccess(session)
{
    public bool CanRead=>Has("COMERCIAL.VER","CLIENTES.VER");
    public bool CanManage=>CanRead&&Has("COMERCIAL.PROSPECTAR");
    public bool CanExclude=>CanRead&&Has("COMERCIAL.EXCLUSIONES");
    public bool CanCampaign=>CanRead&&Has("COMERCIAL.CAMPANIAS_CREAR");
    public bool CanApprove=>CanRead&&Has("COMERCIAL.CAMPANIAS_APROBAR");
    public bool CanConvert=>CanManage&&Has("VENTAS.VER");
    private async Task<T> Guard<T>(Func<Task<T>> read)
    {var rev=CurrentSession is ISessionController s?s.Revision:0;var result=await read();if(!CanRead||CurrentSession is ISessionController c&&rev!=c.Revision)throw new UnauthorizedAccessException("La sesión cambió.");return result;}
    private void Query(MvpFilter f,string search=""){Read("COMERCIAL.VER","CLIENTES.VER");Page(f);if(search is null||search.Length>200)throw new ArgumentException("Búsqueda máxima de 200 caracteres.");}
    public Task<MvpPage<OnlineProspectData>> SearchAsync(string search,string? state,MvpFilter f,CancellationToken ct=default){Query(f,search);if(state is not null&&!OnlineValidator.States.Contains(state))throw new ArgumentException("Estado desconocido.");return Guard(()=>repository.OnlineProspectsAsync(search.Trim(),state,f,UserId,ct));}
    public Task<OnlineProspectData?> ReadAsync(long id,CancellationToken ct=default){Query(new());Id(id);return Guard(()=>repository.OnlineProspectAsync(id,UserId,ct));}
    public Task<MvpPage<OnlineSourceData>> SourcesAsync(long id,MvpFilter f,CancellationToken ct=default){Query(f);Id(id);return Guard(()=>repository.OnlineSourcesAsync(id,f,UserId,ct));}
    public Task<MvpPage<OnlineMatch>> CheckMatchesAsync(OnlineProspectInput data,long excludeId,MvpFilter f,CancellationToken ct=default){Query(f);if(excludeId<0||OnlineValidator.Bounds(data) is not null)throw new ArgumentException("Revisa los datos para comparar.");return Guard(()=>repository.OnlineCheckMatchesAsync(OnlineValidator.Copy(data),excludeId,f,UserId,ct));}
    public Task<MvpPage<OnlineMatch>> MatchesAsync(long id,MvpFilter f,CancellationToken ct=default){Query(f);Id(id);return Guard(()=>repository.OnlineMatchesAsync(id,f,UserId,ct));}
    public Task<MvpPage<OnlineEvent>> HistoryAsync(long id,MvpFilter f,CancellationToken ct=default){Query(f);Id(id);return Guard(()=>repository.OnlineHistoryAsync(id,f,UserId,ct));}
    public Task<MvpPage<OnlineExclusion>> ExclusionsAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);return Guard(()=>repository.OnlineExclusionsAsync(search.Trim(),f,UserId,ct));}
    public Task<MvpPage<SaleData>> SalesAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);Read("VENTAS.VER");return Guard(()=>repository.OnlineSalesAsync(search.Trim(),f,UserId,ct));}
    public Task<Result<MvpReceipt>> SaveAsync(object r,CancellationToken ct=default)
    {
        var allowed=r switch{ExcludeOnlineContact or BlockOnlineProspect=>CanExclude,ConvertOnlineProspect=>CanConvert,SaveOnlineTemplate or CreateOnlineCampaign=>CanCampaign,TransitionOnlineCampaign t=>t.Action=="PREPARAR"?CanCampaign:CanApprove,_=>CanManage};
        if(!allowed)return Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso para esta operación Online."));
        if(OnlineValidator.Validate(r) is {} error)return Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error));
        return repository.SaveOnlineAsync(OnlineValidator.Freeze(r),UserId,WorkstationId,ct);
    }
}
