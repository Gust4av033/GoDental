using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
internal sealed class OnlineUiRepository:IOnlineRepository
{
    public bool DiscoveryDenied {get;set;}
    public List<object> Writes{get;}=[];
    public bool LoseResponse{get;set;}
    public Task<MvpPage<OnlineProspectData>>? PendingPreview {get;set;}
    public Task<OnlineProspectData?>? PendingDetail{get;set;}
    public Task<MvpPage<OnlineProspectData>>? PendingSearch{get;set;}
    public Task<MvpPage<OnlineMatch>>? PendingMatch{get;set;}
    public OnlineProspectData Prospect{get;}=new(){Id=1,Name="Negocio de prueba",Phone="7000-0000",Email="negocio@example.com",Website="https://negocio.example.com/",Department="San Salvador",Municipality="San Salvador Centro",State="REVISADO",RowVersion=new byte[8]};
    public SaleData Sale{get;}=new(){Id=1,Number="PRUEBA-001",CustomerId=1,CustomerSnapshot="{\"NombreRazonSocial\":\"Cliente de prueba\"}",State="CONFIRMADA",Total=10,RowVersion=new byte[8]};
    public Task<MvpPage<OnlineProspectData>> OnlineProspectsAsync(string s,string? state,MvpFilter f,int u,CancellationToken ct)=>PendingSearch??Task.FromResult(new MvpPage<OnlineProspectData>([Prospect],1));
    public Task<OnlineProspectData?> OnlineProspectAsync(long id,int u,CancellationToken ct)=>PendingDetail??Task.FromResult<OnlineProspectData?>(Prospect);
    public Task<MvpPage<OnlineSourceData>> OnlineSourcesAsync(long id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineSourceData>([new(){Id=1,Type="WEB",Url="https://negocio.example.com/contacto",Obtained=DateTimeOffset.UtcNow,Description="Contacto empresarial público sintético"}],1));
    public Task<MvpPage<OnlineMatch>> OnlineMatchesAsync(long id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineMatch>([new(){Entity="CLIENTE",Id=1,Name="Cliente de prueba",State="ACTIVO",Match="Correo"}],1));
    public Task<MvpPage<OnlineMatch>> OnlineCheckMatchesAsync(OnlineProspectInput d,long id,MvpFilter f,int u,CancellationToken ct)=>PendingMatch??OnlineMatchesAsync(id,f,u,ct);
    public Task<MvpPage<OnlineEvent>> OnlineHistoryAsync(long id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineEvent>([new(){Date=DateTimeOffset.UtcNow,User="Usuario de prueba",Action="ONLINE_PROSPECTO",Reason="Alta sintética"}],1));
    public Task<MvpPage<OnlineExclusion>> OnlineExclusionsAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineExclusion>([new(){Id=1,Channel="EMAIL",Identifier="excluido@example.com",Active=true,Reason="No contactar",Date=DateTimeOffset.UtcNow}],1));
    public Task<MvpPage<SaleData>> OnlineSalesAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<SaleData>([Sale],1));
    public Task<MvpPage<OnlineZone>> OnlineZonesAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineZone>([new(){Id=1,Department="San Salvador",Municipality="San Salvador Centro",Prospects=1,RowVersion=new byte[8]}],1));
    public Task<MvpPage<OnlineSegment>> OnlineSegmentsAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineSegment>([new(){Id=1,Name="Negocios de prueba",Channel="EMAIL",RowVersion=new byte[8]}],1));
    public Task<MvpPage<OnlineProspectData>> OnlineSegmentPreviewAsync(OnlineSegmentFilter filter,MvpFilter f,int u,CancellationToken ct)=>PendingPreview??Task.FromResult(new MvpPage<OnlineProspectData>([Prospect],1));
    public Task<MvpPage<OnlineTemplate>> OnlineTemplatesAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineTemplate>([new(){Id=1,Name="Presentación",Channel="EMAIL",Content="Hola {nombre}, conoce nuestro catálogo.",RowVersion=new byte[8]}],1));
    public Task<MvpPage<OnlineCampaign>> OnlineCampaignsAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineCampaign>([new(){Id=1,Name="Campaña de prueba",Channel="EMAIL",State="PENDIENTE_APROBACION",SegmentId=1,Segment="Negocios de prueba",CriteriaJson="{}",Content="Hola {nombre}, conoce nuestro catálogo.",Recipients=1,RowVersion=new byte[8]}],1));
    public Task<MvpPage<OnlineRecipient>> OnlineRecipientsAsync(long id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineRecipient>([new(){Id=1,Name="Negocio de prueba",Contact="negocio@example.com",State="PENDIENTE"}],1));
    public Task<MvpPage<OnlineFollowUp>> OnlineFollowUpsAsync(string s,long? id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<OnlineFollowUp>([new(){Id=1,ProspectId=1,Name="Negocio de prueba",Channel="EMAIL",Result="INTERESADO",Date=DateTimeOffset.UtcNow,NextDate=DateTime.Today.AddDays(2),Note="Solicita información del catálogo, ejemplo sintético.",User="Usuario de prueba"}],1));
    public Task AuthorizeOnlineDiscoveryAsync(int user,CancellationToken ct)=>DiscoveryDenied?Task.FromException(new UnauthorizedAccessException()):Task.CompletedTask;
    public Task<Result<MvpReceipt>> SaveOnlineAsync(object r,int u,int? station,CancellationToken ct){Writes.Add(r);if(LoseResponse)throw new System.IO.IOException("Respuesta perdida en prueba");return Task.FromResult(Result<MvpReceipt>.Success(new(1,GoDental.Application.Validators.OnlineValidator.Identity(r).Id)));}
}
