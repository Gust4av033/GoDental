using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IOnlineRepository
{
    Task<MvpPage<OnlineProspectData>> OnlineProspectsAsync(string search,string? state,MvpFilter f,int user,CancellationToken ct);
    Task<OnlineProspectData?> OnlineProspectAsync(long id,int user,CancellationToken ct);
    Task<MvpPage<OnlineSourceData>> OnlineSourcesAsync(long id,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineMatch>> OnlineCheckMatchesAsync(OnlineProspectInput data,long excludeId,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineMatch>> OnlineMatchesAsync(long id,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineExclusion>> OnlineExclusionsAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineEvent>> OnlineHistoryAsync(long id,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<SaleData>> OnlineSalesAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineZone>> OnlineZonesAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineSegment>> OnlineSegmentsAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineProspectData>> OnlineSegmentPreviewAsync(OnlineSegmentFilter filter,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineTemplate>> OnlineTemplatesAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineCampaign>> OnlineCampaignsAsync(string search,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineRecipient>> OnlineRecipientsAsync(long campaignId,MvpFilter f,int user,CancellationToken ct);
    Task<MvpPage<OnlineFollowUp>> OnlineFollowUpsAsync(string search,long? prospectId,MvpFilter f,int user,CancellationToken ct);
    Task AuthorizeOnlineDiscoveryAsync(int user,CancellationToken ct);
    Task<Result<MvpReceipt>> SaveOnlineAsync(object request,int user,int? station,CancellationToken ct);
}
