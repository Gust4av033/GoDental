using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class OnlineViewModel:MvpWorkspaceViewModel
{
    private readonly OnlineService service;private readonly IMvpDialogService dialogs;
    public ObservableCollection<OnlineProspectData> Items{get;}=[];
    public ObservableCollection<OnlineSourceData> Sources{get;}=[];
    public ObservableCollection<OnlineMatch> Matches{get;}=[];
    public ObservableCollection<OnlineEvent> History{get;}=[];
    public ObservableCollection<OnlineExclusion> Exclusions{get;}=[];
    public ObservableCollection<SaleData> Sales{get;}=[];
    public IReadOnlyList<string> States{get;}=["TODOS",..OnlineValidator.States];
    [ObservableProperty]private string search="";
    [ObservableProperty]private string state="TODOS";
    [ObservableProperty]private string exclusionSearch="";
    [ObservableProperty]private string saleSearch="";
    [ObservableProperty]private OnlineProspectData? selected;
    [ObservableProperty]private OnlineProspectData? detail;
    [ObservableProperty]private SaleData? selectedSale;
    [ObservableProperty]private int page=1;
    [ObservableProperty]private int total;
    [ObservableProperty]private int sourcePage=1;
    [ObservableProperty]private int sourceTotal;
    [ObservableProperty]private int matchPage=1;
    [ObservableProperty]private int matchTotal;
    [ObservableProperty]private int historyPage=1;
    [ObservableProperty]private int historyTotal;
    [ObservableProperty]private int exclusionPage=1;
    [ObservableProperty]private int exclusionTotal;
    [ObservableProperty]private int salePage=1;
    [ObservableProperty]private int saleTotal;
    public override bool CanRead=>service.CanRead;
    public bool CanManage=>CanEdit&&service.CanManage;
    public bool CanChange=>CanManage&&Detail is{State:not "CONVERTIDO"};
    public bool CanExclude=>CanEdit&&service.CanExclude;
    public bool CanConvert=>CanEdit&&service.CanConvert&&Detail is{State:not("CONVERTIDO" or "DESCARTADO")}&&SelectedSale?.CustomerId is not null;
    public bool CanReadSales=>CanRead&&service.CanConvert;
    public string PageLabel=>$"{Total} prospectos · Página {Page}";
    public string SourcePageLabel=>$"{SourceTotal} fuentes · Página {SourcePage}";
    public string MatchPageLabel=>$"{MatchTotal} coincidencias · Página {MatchPage}";
    public string HistoryPageLabel=>$"{HistoryTotal} eventos · Página {HistoryPage}";
    public string ExclusionPageLabel=>$"{ExclusionTotal} exclusiones · Página {ExclusionPage}";
    public string SalePageLabel=>$"{SaleTotal} compras confirmadas · Página {SalePage}";
    public OnlineSourceData? PrimarySource=>Sources.FirstOrDefault();
    public string DetailMessage=>Detail is null?"Selecciona un prospecto y consulta su ficha.":$"Prospecto {Detail.Id} · {Detail.State} · {Detail.SuppressionLabel}";
    public OnlineViewModel(OnlineService service,IMvpDialogService dialogs,ISessionController session,MvpOperationService operations,ILogger<OnlineViewModel> logger):base(session,operations,logger){this.service=service;this.dialogs=dialogs;Sources.CollectionChanged+=(_,_)=>OnPropertyChanged(nameof(PrimarySource));}
    partial void OnSelectedChanged(OnlineProspectData? value){Detail=null;Sources.Clear();Matches.Clear();History.Clear();SourceTotal=MatchTotal=HistoryTotal=0;SourcePage=MatchPage=HistoryPage=1;Labels();}
    partial void OnDetailChanged(OnlineProspectData? value){OnPropertyChanged(nameof(DetailMessage));OnPropertyChanged(nameof(CampaignPreview));PermissionsChanged();}
    partial void OnSelectedSaleChanged(SaleData? value)=>OnPropertyChanged(nameof(CanConvert));
    protected override void PermissionsChanged(){foreach(var n in new[]{nameof(CanManage),nameof(CanChange),nameof(CanExclude),nameof(CanConvert),nameof(CanReadSales),nameof(CanPlanCampaign),nameof(CanApproveCampaign)})OnPropertyChanged(n);}
    private void Labels(){foreach(var n in new[]{nameof(PageLabel),nameof(SourcePageLabel),nameof(MatchPageLabel),nameof(HistoryPageLabel),nameof(ExclusionPageLabel),nameof(SalePageLabel)})OnPropertyChanged(n);}
    protected override void Reset(){Items.Clear();Sources.Clear();Matches.Clear();History.Clear();Exclusions.Clear();Sales.Clear();Selected=null;Detail=null;SelectedSale=null;Page=SourcePage=MatchPage=HistoryPage=ExclusionPage=SalePage=1;Total=SourceTotal=MatchTotal=HistoryTotal=ExclusionTotal=SaleTotal=0;ResetPlanning();Labels();}
    [RelayCommand]private Task ReloadAsync(){Page=1;return ReadAsync(LoadItems);}
    private async Task LoadItems(long rev){var s=Search;var state=State;var page=Page;var result=await service.SearchAsync(s,state=="TODOS"?null:state,new(page));if(!Current(rev)||s!=Search||state!=State||page!=Page)return;var id=Selected?.Id;Items.Clear();foreach(var r in result.Items)Items.Add(r);Total=result.Total;Selected=Items.FirstOrDefault(r=>r.Id==id);Labels();StatusMessage=Total==0?"No hay prospectos para este filtro.":"Selecciona un prospecto para consultar su ficha y continuar el seguimiento.";}
    [RelayCommand]private Task OpenAsync()=>ReadAsync(async rev=>{if(Selected is {} p){await LoadDetail(p.Id,rev);if(Current(rev)&&Detail?.Id==p.Id)SectionIndex=2;}});
    private async Task LoadDetail(long id,long rev){Detail=null;Sources.Clear();Matches.Clear();History.Clear();var result=await service.ReadAsync(id);if(!Current(rev)||Selected?.Id!=id)return;Detail=result;SourcePage=MatchPage=HistoryPage=1;if(result is null)return;await LoadSources(rev);await LoadMatches(rev);await LoadHistory(rev);}
    private async Task LoadSources(long rev){if(Detail is not {} d)return;var page=SourcePage;var r=await service.SourcesAsync(d.Id,new(page));if(!Current(rev)||Detail?.Id!=d.Id||SourcePage!=page)return;Sources.Clear();foreach(var row in r.Items)Sources.Add(row);SourceTotal=r.Total;Labels();}
    private async Task LoadMatches(long rev){if(Detail is not {} d)return;var page=MatchPage;var r=await service.MatchesAsync(d.Id,new(page));if(!Current(rev)||Detail?.Id!=d.Id||MatchPage!=page)return;Matches.Clear();foreach(var row in r.Items)Matches.Add(row);MatchTotal=r.Total;Labels();}
    private async Task LoadHistory(long rev){if(Detail is not {} d)return;var page=HistoryPage;var r=await service.HistoryAsync(d.Id,new(page));if(!Current(rev)||Detail?.Id!=d.Id||HistoryPage!=page)return;History.Clear();foreach(var row in r.Items)History.Add(row);HistoryTotal=r.Total;Labels();}
    [RelayCommand]private Task FindExclusionsAsync(){ExclusionPage=1;return ReadAsync(LoadExclusions);}
    private async Task LoadExclusions(long rev){var s=ExclusionSearch;var page=ExclusionPage;var r=await service.ExclusionsAsync(s,new(page));if(!Current(rev)||s!=ExclusionSearch||page!=ExclusionPage)return;Exclusions.Clear();foreach(var row in r.Items)Exclusions.Add(row);ExclusionTotal=r.Total;Labels();StatusMessage=ExclusionTotal==0?"No hay exclusiones para esta búsqueda. Esto no acredita consentimiento.":"Exclusiones centrales consultadas.";}
    [RelayCommand]private Task FindSalesAsync(){SalePage=1;return !CanReadSales?Task.CompletedTask:ReadAsync(LoadSales);}
    private async Task LoadSales(long rev){if(!CanReadSales)return;var s=SaleSearch;var page=SalePage;var r=await service.SalesAsync(s,new(page));if(!Current(rev)||s!=SaleSearch||page!=SalePage)return;Sales.Clear();foreach(var row in r.Items)Sales.Add(row);SelectedSale=null;SaleTotal=r.Total;Labels();}
    [RelayCommand]private async Task NextAsync(){if(IsBusy||Page*30>=Total)return;Page++;await ReadAsync(LoadItems);}
    [RelayCommand]private async Task PreviousAsync(){if(IsBusy||Page==1)return;Page--;await ReadAsync(LoadItems);}
    [RelayCommand]private async Task NextSourceAsync(){if(IsBusy||SourcePage*30>=SourceTotal)return;SourcePage++;await ReadAsync(LoadSources);}
    [RelayCommand]private async Task PreviousSourceAsync(){if(IsBusy||SourcePage==1)return;SourcePage--;await ReadAsync(LoadSources);}
    [RelayCommand]private async Task NextMatchAsync(){if(IsBusy||MatchPage*30>=MatchTotal)return;MatchPage++;await ReadAsync(LoadMatches);}
    [RelayCommand]private async Task PreviousMatchAsync(){if(IsBusy||MatchPage==1)return;MatchPage--;await ReadAsync(LoadMatches);}
    [RelayCommand]private async Task NextHistoryAsync(){if(IsBusy||HistoryPage*30>=HistoryTotal)return;HistoryPage++;await ReadAsync(LoadHistory);}
    [RelayCommand]private async Task PreviousHistoryAsync(){if(IsBusy||HistoryPage==1)return;HistoryPage--;await ReadAsync(LoadHistory);}
    [RelayCommand]private async Task NextExclusionAsync(){if(IsBusy||ExclusionPage*30>=ExclusionTotal)return;ExclusionPage++;await ReadAsync(LoadExclusions);}
    [RelayCommand]private async Task PreviousExclusionAsync(){if(IsBusy||ExclusionPage==1)return;ExclusionPage--;await ReadAsync(LoadExclusions);}
    [RelayCommand]private async Task NextSaleAsync(){if(IsBusy||SalePage*30>=SaleTotal)return;SalePage++;await ReadAsync(LoadSales);}
    [RelayCommand]private async Task PreviousSaleAsync(){if(IsBusy||SalePage==1)return;SalePage--;await ReadAsync(LoadSales);}
    private async Task Show(MvpFormModel f){using(f){IsEditing=true;try{await dialogs.ShowAsync(f);}finally{IsEditing=false;}}}
    private Task Commit(object r)=>Operation.StartAsync(r,OnlineValidator.Identity(r).Id,q=>service.SaveAsync(q),receipt=>ReadAsync(async rev=>{await LoadItems(rev);if(!Current(rev))return;Selected=Items.FirstOrDefault(p=>p.Id==receipt.Id);if(Selected is not null)await LoadDetail(receipt.Id,rev);}));
    [RelayCommand]private Task CreateAsync()=>CanManage?EditForm(null):Task.CompletedTask;
    [RelayCommand]private Task EditAsync()=>CanChange?EditForm(Detail):Task.CompletedTask;
    private Task EditForm(OnlineProspectData? p)
    {
        var draft=new OnlineDraftModel(service,p);var version=p?.RowVersion.ToArray()??[];
        return Show(new MvpFormModel(p is null?"Registrar prospecto público":"Editar prospecto","Guardar prospecto",Operation,f=>Commit(new SaveOnlineProspect(Guid.NewGuid(),p?.Id??0,version,draft.Snapshot(),p is null?draft.Source():null,f.Text("reason")))){Body=draft,Description="Sólo datos empresariales públicos. Registra fuente y fecha, compara coincidencias y no incluyas secretos. No acredita consentimiento ni envía mensajes."}.Input("reason","Motivo"));
    }
    [RelayCommand]private Task AddSourceAsync()
    {
        if(!CanManage||Detail is not {} p)return Task.CompletedTask;var version=p.RowVersion.ToArray();var draft=new OnlineDraftModel(service,p,true);
        return Show(new MvpFormModel("Añadir fuente pública","Registrar fuente",Operation,f=>Commit(new AddOnlineSource(Guid.NewGuid(),p.Id,version,draft.Source(),f.Text("reason")))){Body=draft,Description="Conserva origen y fecha. No se descarga la URL ni se reemplazan fuentes históricas."}.Input("reason","Motivo"));
    }
    public sealed record Choice(string Code,string Name);
    [RelayCommand]private Task ChangeStateAsync()
    {
        if(!CanChange||Detail is not {} p)return Task.CompletedTask;var version=p.RowVersion.ToArray();Choice[] choices=[new("REVISADO","Revisado internamente"),new("DESCARTADO","Descartado")];
        return Show(new MvpFormModel("Estado interno del prospecto","Registrar estado",Operation,f=>Commit(new ChangeOnlineState(Guid.NewGuid(),p.Id,version,f.Choice<Choice>("state").Code,f.Text("reason")))){Description="No se registra un contacto ni se envían mensajes."}.Select("state","Resultado",choices,choices[0]).Input("reason","Motivo"));
    }
    [RelayCommand]private Task BlockAsync()
    {
        if(!CanExclude||Detail is not {} p)return Task.CompletedTask;var version=p.RowVersion.ToArray();
        return Show(new MvpFormModel("No contactar al prospecto","Registrar bloqueo",Operation,f=>Commit(new BlockOnlineProspect(Guid.NewGuid(),p.Id,version,f.Text("reason")))){Description=$"{p.Name}. Bloqueo global y exclusión de sus contactos conocidos. Este bloque no permite levantar exclusiones. No impide comprar."}.Input("reason","Motivo de no contactar"));
    }
    [RelayCommand]private Task AddExclusionAsync()
    {
        if(!CanExclude)return Task.CompletedTask;Choice[] channels=[new("WHATSAPP","WhatsApp · teléfono con prefijo opcional SV"),new("EMAIL","Correo electrónico"),new("INSTAGRAM","Instagram · URL HTTPS del perfil"),new("FACEBOOK","Facebook · URL HTTPS del perfil")];
        return Show(new MvpFormModel("Lista central de no contactar","Registrar exclusión",Operation,async f=>{var r=new ExcludeOnlineContact(Guid.NewGuid(),f.Choice<Choice>("channel").Code,f.Text("identifier"),f.Text("reason"));await Operation.StartAsync(r,r.OperationId,q=>service.SaveAsync(q),_=>ReadAsync(async rev=>{await LoadExclusions(rev);if(Current(rev)&&Selected is {} p)await LoadDetail(p.Id,rev);}));}){Description="Alta o reactivación restrictiva. No elimina exclusiones ni habilita campañas."}.Select("channel","Canal",channels,channels[0]).Input("identifier","Identificador público").Input("reason","Motivo"));
    }
    [RelayCommand]private Task ConvertAsync()
    {
        if(!CanConvert||Detail is not {} p||SelectedSale is not {} sale)return Task.CompletedTask;var pv=p.RowVersion.ToArray();var sv=sale.RowVersion.ToArray();
        return Show(new MvpFormModel("Vincular compra confirmada","Convertir prospecto",Operation,f=>Commit(new ConvertOnlineProspect(Guid.NewGuid(),p.Id,pv,sale.Id,sv,f.Text("reason")))){Description=$"Confirma que {p.Name} corresponde al cliente de la venta {sale.Number}, USD {sale.Total:N2}. Conserva fuente y exclusiones. No crea cliente, crédito ni otra venta."}.Input("reason","Motivo / identidad revisada"));
    }
}
