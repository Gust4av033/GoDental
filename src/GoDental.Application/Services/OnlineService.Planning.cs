using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed partial class OnlineService
{
    public Task<MvpPage<OnlineZone>> ZonesAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);return Guard(()=>repository.OnlineZonesAsync(search.Trim(),f,UserId,ct));}
    public Task<MvpPage<OnlineSegment>> SegmentsAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);return Guard(()=>repository.OnlineSegmentsAsync(search.Trim(),f,UserId,ct));}
    public Task<MvpPage<OnlineProspectData>> PreviewAsync(OnlineSegmentFilter filter,MvpFilter f,CancellationToken ct=default){Query(f);if(OnlineValidator.FilterError(filter) is {} error)throw new ArgumentException(error);return Guard(()=>repository.OnlineSegmentPreviewAsync(filter,f,UserId,ct));}
    public Task<MvpPage<OnlineTemplate>> TemplatesAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);return Guard(()=>repository.OnlineTemplatesAsync(search.Trim(),f,UserId,ct));}
    public Task<MvpPage<OnlineCampaign>> CampaignsAsync(string search,MvpFilter f,CancellationToken ct=default){Query(f,search);return Guard(()=>repository.OnlineCampaignsAsync(search.Trim(),f,UserId,ct));}
    public Task<MvpPage<OnlineRecipient>> RecipientsAsync(long id,MvpFilter f,CancellationToken ct=default){Query(f);Id(id);return Guard(()=>repository.OnlineRecipientsAsync(id,f,UserId,ct));}
    public Task<MvpPage<OnlineFollowUp>> FollowUpsAsync(string search,long? id,MvpFilter f,CancellationToken ct=default){Query(f,search);if(id is not null)Id(id.Value);return Guard(()=>repository.OnlineFollowUpsAsync(search.Trim(),id,f,UserId,ct));}
}
