using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class OnlineViewModel
{
    [ObservableProperty]private int sectionIndex=1;
    [ObservableProperty]private OnlineZone? selectedZone;
    [ObservableProperty]private OnlineSegment? selectedSegment;
    [ObservableProperty]private OnlineTemplate? selectedTemplate;
    [ObservableProperty]private OnlineCampaign? selectedCampaign;
    public ObservableCollection<OnlineProspectData> Preview {get;}=[];
    public bool CanPlanCampaign=>CanEdit&&service.CanCampaign;
    public bool CanApproveCampaign=>CanEdit&&service.CanApprove;
    public string CampaignMessage=>SelectedCampaign is not {} c?"Selecciona una campaña desde Campañas.":$"{c.Name} · {c.State} · {c.Recipients} preparados · {c.Suppressed} excluidos o modificados. Aprobación: {c.ApprovedBy??"Pendiente"}.";
    public string CampaignPreview=>SelectedCampaign?.Content?.Replace("{nombre}",Detail?.Name??"Negocio de ejemplo",StringComparison.Ordinal)??"Selecciona una campaña con mensaje guardado.";
    partial void OnSelectedCampaignChanged(OnlineCampaign? value){Recipients.Clear();RecipientTotal=0;RecipientPage=1;OnPropertyChanged(nameof(RecipientPageLabel));OnPropertyChanged(nameof(CampaignMessage));OnPropertyChanged(nameof(CampaignPreview));}
    partial void OnSelectedSegmentChanged(OnlineSegment? value){Preview.Clear();PreviewTotal=0;PreviewPage=1;OnPropertyChanged(nameof(PreviewPageLabel));}
    private Task PlanningCommit(object request,Func<long,Task> refresh)=>Operation.StartAsync(request,OnlineValidator.Identity(request).Id,r=>service.SaveAsync(r),_=>ReadAsync(refresh));
    [RelayCommand]private Task NewZoneAsync()=>CanManage?ZoneForm(null):Task.CompletedTask;
    [RelayCommand]private Task EditZoneAsync()=>CanManage&&SelectedZone is {} z?ZoneForm(z):Task.CompletedTask;
    private Task ZoneForm(OnlineZone? z)
    {
        var id=z?.Id??0;var version=z?.RowVersion.ToArray()??[];
        var draft=new OnlinePlanningDraftModel{IsZone=true,Location=new(new(){Department=z?.Department,Municipality=z?.Municipality}),Priority=(z?.Priority??100).ToString(),Active=z?.Active??true};
        return Show(new MvpFormModel("Zona de prospección","Guardar zona",Operation,f=>{var l=draft.Location.Apply(new());return PlanningCommit(new SaveOnlineZone(Guid.NewGuid(),id,version,l.Department??"",l.Municipality,draft.PriorityValue(),draft.Active,f.Text("reason")),LoadZones);}){Body=draft,Description="Organiza el territorio de El Salvador y su prioridad. No inicia búsquedas externas."}.Input("reason","Motivo"));
    }
    [RelayCommand]private Task NewSegmentAsync()=>CanManage?SegmentForm(null):Task.CompletedTask;
    [RelayCommand]private Task EditSegmentAsync()=>CanManage&&SelectedSegment is {} s?SegmentForm(s):Task.CompletedTask;
    private Task SegmentForm(OnlineSegment? s)
    {
        var id=s?.Id??0;var version=s?.RowVersion.ToArray()??[];
        var draft=new OnlinePlanningDraftModel{IsSegment=true,Name=s?.Name??"",Location=new(new(){Department=s?.Department,Municipality=s?.Municipality}),Channel=s?.Channel??"WHATSAPP",Type=s?.Type??"",State=s?.State??"TODOS",Source=s?.Source??"TODAS",Active=s?.Active??true};
        return Show(new MvpFormModel("Segmentación de prospectos","Guardar segmento",Operation,f=>PlanningCommit(new SaveOnlineSegment(Guid.NewGuid(),id,version,draft.Name,draft.Filter(),draft.Active,f.Text("reason")),LoadSegments)){Body=draft,Description="Guarda filtros reutilizables; revisa la vista previa antes de crear una campaña."}.Input("reason","Motivo"));
    }
    [RelayCommand]private Task NewTemplateAsync()=>CanPlanCampaign?TemplateForm(null):Task.CompletedTask;
    [RelayCommand]private Task EditTemplateAsync()=>CanPlanCampaign&&SelectedTemplate is {} t?TemplateForm(t):Task.CompletedTask;
    private Task TemplateForm(OnlineTemplate? t)
    {
        var id=t?.Id??0;var version=t?.RowVersion.ToArray()??[];
        var draft=new OnlinePlanningDraftModel{IsTemplate=true,Name=t?.Name??"",Channel=t?.Channel??"WHATSAPP",Content=t?.Content??"Hola {nombre}, ",Active=t?.Active??true};
        return Show(new MvpFormModel("Plantilla de mensaje","Guardar plantilla",Operation,f=>PlanningCommit(new SaveOnlineTemplate(Guid.NewGuid(),id,version,draft.Name,draft.Channel,draft.Content,draft.Active,f.Text("reason")),LoadTemplates)){Body=draft,Description="Texto plano. Vista previa sin envío; el único marcador admitido es {nombre}."}.Input("reason","Motivo"));
    }
    [RelayCommand]private Task NewCampaignAsync()
    {
        if(!CanPlanCampaign||SelectedSegment is not {} s||SelectedTemplate is not {} t){StatusMessage="Selecciona primero un segmento en Segmentación y una plantilla del mismo canal en Plantillas.";return Task.CompletedTask;}
        if(!s.Active||!t.Active||s.Channel!=t.Channel){StatusMessage="El segmento y la plantilla deben estar activos y usar el mismo canal.";return Task.CompletedTask;}
        var sv=s.RowVersion.ToArray();var tv=t.RowVersion.ToArray();var sid=s.Id;var tid=t.Id;
        return Show(new MvpFormModel("Nueva campaña","Crear borrador",Operation,f=>PlanningCommit(new CreateOnlineCampaign(Guid.NewGuid(),f.Text("name"),sid,sv,tid,tv,f.Text("reason")),LoadCampaigns)){Description=$"Segmento: {s.Name} · plantilla: {t.Name} · {s.Channel}. Se guarda una copia de los criterios y del mensaje para revisión."}.Input("name","Nombre de la campaña").Input("reason","Motivo"));
    }
    [RelayCommand]private Task PrepareCampaignAsync()=>CampaignTransition("PREPARAR");
    [RelayCommand]private Task ApproveCampaignAsync()=>CampaignTransition("APROBAR");
    [RelayCommand]private Task ReturnCampaignAsync()=>CampaignTransition("DEVOLVER");
    [RelayCommand]private Task CancelCampaignAsync()=>CampaignTransition("CANCELAR");
    private Task CampaignTransition(string action)
    {
        if((action=="PREPARAR"?!CanPlanCampaign:!CanApproveCampaign)||SelectedCampaign is not {} c)return Task.CompletedTask;
        var version=c.RowVersion.ToArray();var id=c.Id;
        return Show(new MvpFormModel("Revisión de campaña",action switch{"PREPARAR"=>"Preparar para aprobación","APROBAR"=>"Aprobar preparación","DEVOLVER"=>"Volver a borrador",_=>"Cancelar campaña"},Operation,f=>PlanningCommit(new TransitionOnlineCampaign(Guid.NewGuid(),id,version,action,f.Text("reason")),async rev=>{await LoadCampaigns(rev);if(Current(rev)){SelectedCampaign=Campaigns.FirstOrDefault(x=>x.Id==id);if(SelectedCampaign is not null)await LoadRecipients(rev);}})){Description=$"{c.Name} · {c.State}. Preparar selecciona contactos actuales; volver a borrador descarta esa selección. Aprobar revisa exclusiones y conserva el mensaje. Ninguna acción envía comunicaciones ni acredita consentimiento."}.Input("reason","Motivo de la revisión"));
    }
    [RelayCommand]private Task RecordFollowUpAsync()
    {
        if(!CanManage||Detail is not {} p||p.State=="CONVERTIDO"){StatusMessage="Consulta primero la ficha del prospecto que vas a gestionar.";return Task.CompletedTask;}
        var id=p.Id;var version=p.RowVersion.ToArray();var results=OnlineValidator.FollowUpResults.Select(s=>new Choice(s,s)).ToArray();var channels=OnlineValidator.Channels.Select(s=>new Choice(s,s)).ToArray();
        var form=new MvpFormModel("Respuestas y seguimiento","Registrar gestión",Operation,f=>PlanningCommit(new RecordOnlineFollowUp(Guid.NewGuid(),id,version,f.Choice<Choice>("result").Code,f.Choice<Choice>("channel").Code,f.Date("date")??throw new ArgumentException("Selecciona la fecha de la interacción."),f.Date("next"),f.Text("note"),f.Text("reason")),async rev=>{await LoadFollowUps(rev);if(Current(rev)&&Selected?.Id==id)await LoadDetail(id,rev);})){Description=$"{p.Name}. Registra una interacción ya ocurrida fuera de GoDental. No envía mensajes; una nota o respuesta no elimina exclusiones."}.Select("result","Resultado",results,results.Last()).Select("channel","Canal",channels,channels[0]).Calendar("date","Fecha de la interacción").Calendar("next","Próxima gestión · opcional").Input("note","Nota / respuesta").Input("reason","Motivo del registro");
        form.Fields.Single(x=>x.Key=="date").Date=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
        return Show(form);
    }
    [RelayCommand]private Task OpenFollowUpAsync()=>ReadAsync(async rev=>{if(SelectedFollowUp is not {} h)return;var p=await service.ReadAsync(h.ProspectId);if(!Current(rev)||SelectedFollowUp?.ProspectId!=h.ProspectId)return;Selected=p;if(p is not null){await LoadDetail(p.Id,rev);if(Current(rev))SectionIndex=2;}});
    [ObservableProperty]private OnlineFollowUp? selectedFollowUp;
    public ObservableCollection<OnlineZone> Zones {get;}=[];
    [ObservableProperty]private string zoneSearch="";
    [ObservableProperty]private int zonePage=1;
    [ObservableProperty]private int zoneTotal;
    public string ZonePageLabel=>$"{ZoneTotal} registros · Página {ZonePage}";
    [RelayCommand]private Task FindZonesAsync(){ZonePage=1;return ReadAsync(LoadZones);}
    private async Task LoadZones(long rev)
    {
        var search=ZoneSearch;var page=ZonePage;var r=await service.ZonesAsync(search,new(page));
        if(!Current(rev)||search!=ZoneSearch||page!=ZonePage)return;
        var id=SelectedZone?.Id;
        Zones.Clear();foreach(var row in r.Items)Zones.Add(row);ZoneTotal=r.Total;
        SelectedZone=Zones.FirstOrDefault(x=>x.Id==id);
        OnPropertyChanged(nameof(ZonePageLabel));StatusMessage=r.Total==0?"No hay registros para esta búsqueda.":"Datos comerciales consultados.";
    }
    [RelayCommand]private async Task NextZoneAsync(){if(IsBusy||ZonePage*30>=ZoneTotal)return;ZonePage++;await ReadAsync(LoadZones);}
    [RelayCommand]private async Task PreviousZoneAsync(){if(IsBusy||ZonePage==1)return;ZonePage--;await ReadAsync(LoadZones);}
    public ObservableCollection<OnlineSegment> Segments {get;}=[];
    [ObservableProperty]private string segmentSearch="";
    [ObservableProperty]private int segmentPage=1;
    [ObservableProperty]private int segmentTotal;
    public string SegmentPageLabel=>$"{SegmentTotal} registros · Página {SegmentPage}";
    [RelayCommand]private Task FindSegmentsAsync(){SegmentPage=1;return ReadAsync(LoadSegments);}
    private async Task LoadSegments(long rev)
    {
        var search=SegmentSearch;var page=SegmentPage;var r=await service.SegmentsAsync(search,new(page));
        if(!Current(rev)||search!=SegmentSearch||page!=SegmentPage)return;
        var id=SelectedSegment?.Id;
        Segments.Clear();foreach(var row in r.Items)Segments.Add(row);SegmentTotal=r.Total;
        SelectedSegment=Segments.FirstOrDefault(x=>x.Id==id);
        OnPropertyChanged(nameof(SegmentPageLabel));StatusMessage=r.Total==0?"No hay registros para esta búsqueda.":"Datos comerciales consultados.";
    }
    [RelayCommand]private async Task NextSegmentAsync(){if(IsBusy||SegmentPage*30>=SegmentTotal)return;SegmentPage++;await ReadAsync(LoadSegments);}
    [RelayCommand]private async Task PreviousSegmentAsync(){if(IsBusy||SegmentPage==1)return;SegmentPage--;await ReadAsync(LoadSegments);}
    public ObservableCollection<OnlineTemplate> Templates {get;}=[];
    [ObservableProperty]private string templateSearch="";
    [ObservableProperty]private int templatePage=1;
    [ObservableProperty]private int templateTotal;
    public string TemplatePageLabel=>$"{TemplateTotal} registros · Página {TemplatePage}";
    [RelayCommand]private Task FindTemplatesAsync(){TemplatePage=1;return ReadAsync(LoadTemplates);}
    private async Task LoadTemplates(long rev)
    {
        var search=TemplateSearch;var page=TemplatePage;var r=await service.TemplatesAsync(search,new(page));
        if(!Current(rev)||search!=TemplateSearch||page!=TemplatePage)return;
        var id=SelectedTemplate?.Id;
        Templates.Clear();foreach(var row in r.Items)Templates.Add(row);TemplateTotal=r.Total;
        SelectedTemplate=Templates.FirstOrDefault(x=>x.Id==id);
        OnPropertyChanged(nameof(TemplatePageLabel));StatusMessage=r.Total==0?"No hay registros para esta búsqueda.":"Datos comerciales consultados.";
    }
    [RelayCommand]private async Task NextTemplateAsync(){if(IsBusy||TemplatePage*30>=TemplateTotal)return;TemplatePage++;await ReadAsync(LoadTemplates);}
    [RelayCommand]private async Task PreviousTemplateAsync(){if(IsBusy||TemplatePage==1)return;TemplatePage--;await ReadAsync(LoadTemplates);}
    public ObservableCollection<OnlineCampaign> Campaigns {get;}=[];
    [ObservableProperty]private string campaignSearch="";
    [ObservableProperty]private int campaignPage=1;
    [ObservableProperty]private int campaignTotal;
    public string CampaignPageLabel=>$"{CampaignTotal} registros · Página {CampaignPage}";
    [RelayCommand]private Task FindCampaignsAsync(){CampaignPage=1;return ReadAsync(LoadCampaigns);}
    private async Task LoadCampaigns(long rev)
    {
        var search=CampaignSearch;var page=CampaignPage;var r=await service.CampaignsAsync(search,new(page));
        if(!Current(rev)||search!=CampaignSearch||page!=CampaignPage)return;
        var id=SelectedCampaign?.Id;
        Campaigns.Clear();foreach(var row in r.Items)Campaigns.Add(row);CampaignTotal=r.Total;
        SelectedCampaign=Campaigns.FirstOrDefault(x=>x.Id==id);
        OnPropertyChanged(nameof(CampaignPageLabel));StatusMessage=r.Total==0?"No hay registros para esta búsqueda.":"Datos comerciales consultados.";
    }
    [RelayCommand]private async Task NextCampaignAsync(){if(IsBusy||CampaignPage*30>=CampaignTotal)return;CampaignPage++;await ReadAsync(LoadCampaigns);}
    [RelayCommand]private async Task PreviousCampaignAsync(){if(IsBusy||CampaignPage==1)return;CampaignPage--;await ReadAsync(LoadCampaigns);}
    public ObservableCollection<OnlineFollowUp> FollowUps {get;}=[];
    [ObservableProperty]private string followUpSearch="";
    [ObservableProperty]private int followUpPage=1;
    [ObservableProperty]private int followUpTotal;
    public string FollowUpPageLabel=>$"{FollowUpTotal} registros · Página {FollowUpPage}";
    [RelayCommand]private Task FindFollowUpsAsync(){FollowUpPage=1;return ReadAsync(LoadFollowUps);}
    private async Task LoadFollowUps(long rev)
    {
        var search=FollowUpSearch;var page=FollowUpPage;var r=await service.FollowUpsAsync(search,null,new(page));
        if(!Current(rev)||search!=FollowUpSearch||page!=FollowUpPage)return;
        FollowUps.Clear();foreach(var row in r.Items)FollowUps.Add(row);FollowUpTotal=r.Total;
        OnPropertyChanged(nameof(FollowUpPageLabel));StatusMessage=r.Total==0?"No hay registros para esta búsqueda.":"Datos comerciales consultados.";
    }
    [RelayCommand]private async Task NextFollowUpAsync(){if(IsBusy||FollowUpPage*30>=FollowUpTotal)return;FollowUpPage++;await ReadAsync(LoadFollowUps);}
    [RelayCommand]private async Task PreviousFollowUpAsync(){if(IsBusy||FollowUpPage==1)return;FollowUpPage--;await ReadAsync(LoadFollowUps);}
    [ObservableProperty]private int previewPage=1;
    [ObservableProperty]private int previewTotal;
    public string PreviewPageLabel=>$"{PreviewTotal} registros · Página {PreviewPage}";
    [RelayCommand]private Task FindPreviewAsync(){PreviewPage=1;return ReadAsync(LoadPreview);}
    private async Task LoadPreview(long rev)
    {
        if(SelectedSegment is null){StatusMessage="Selecciona primero el registro que deseas consultar.";return;}
        var id=SelectedSegment.Id;var page=PreviewPage;var filter=SelectedSegment.Filter;
        var r=await service.PreviewAsync(filter,new(page));if(!Current(rev)||SelectedSegment?.Id!=id||SelectedSegment.Filter!=filter||PreviewPage!=page)return;
        Preview.Clear();foreach(var row in r.Items)Preview.Add(row);PreviewTotal=r.Total;OnPropertyChanged(nameof(PreviewPageLabel));
    }
    [RelayCommand]private async Task NextPreviewAsync(){if(IsBusy||PreviewPage*30>=PreviewTotal)return;PreviewPage++;await ReadAsync(LoadPreview);}
    [RelayCommand]private async Task PreviousPreviewAsync(){if(IsBusy||PreviewPage==1)return;PreviewPage--;await ReadAsync(LoadPreview);}
    public ObservableCollection<OnlineRecipient> Recipients {get;}=[];
    [ObservableProperty]private int recipientPage=1;
    [ObservableProperty]private int recipientTotal;
    public string RecipientPageLabel=>$"{RecipientTotal} registros · Página {RecipientPage}";
    [RelayCommand]private Task FindRecipientsAsync(){RecipientPage=1;return ReadAsync(LoadRecipients);}
    private async Task LoadRecipients(long rev)
    {
        if(SelectedCampaign is null){StatusMessage="Selecciona primero el registro que deseas consultar.";return;}
        var id=SelectedCampaign.Id;var page=RecipientPage;
        var r=await service.RecipientsAsync(id,new(page));if(!Current(rev)||SelectedCampaign?.Id!=id||RecipientPage!=page)return;
        Recipients.Clear();foreach(var row in r.Items)Recipients.Add(row);RecipientTotal=r.Total;OnPropertyChanged(nameof(RecipientPageLabel));
    }
    [RelayCommand]private async Task NextRecipientAsync(){if(IsBusy||RecipientPage*30>=RecipientTotal)return;RecipientPage++;await ReadAsync(LoadRecipients);}
    [RelayCommand]private async Task PreviousRecipientAsync(){if(IsBusy||RecipientPage==1)return;RecipientPage--;await ReadAsync(LoadRecipients);}
    private void ResetPlanning(){
        Zones.Clear();ZonePage=1;ZoneTotal=0;OnPropertyChanged(nameof(ZonePageLabel));
        Segments.Clear();SegmentPage=1;SegmentTotal=0;OnPropertyChanged(nameof(SegmentPageLabel));
        Templates.Clear();TemplatePage=1;TemplateTotal=0;OnPropertyChanged(nameof(TemplatePageLabel));
        Campaigns.Clear();CampaignPage=1;CampaignTotal=0;OnPropertyChanged(nameof(CampaignPageLabel));
        FollowUps.Clear();FollowUpPage=1;FollowUpTotal=0;OnPropertyChanged(nameof(FollowUpPageLabel));
        Preview.Clear();Recipients.Clear();PreviewPage=RecipientPage=1;PreviewTotal=RecipientTotal=0;OnPropertyChanged(nameof(PreviewPageLabel));OnPropertyChanged(nameof(RecipientPageLabel));SelectedZone=null;SelectedSegment=null;SelectedTemplate=null;SelectedCampaign=null;SelectedFollowUp=null;SectionIndex=1;
    }
}
