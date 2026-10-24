using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class FiscalViewModel:MvpWorkspaceViewModel
{
    private readonly FiscalWorkspaceService fiscal;
    private readonly FiscalPendingService pending;
    private readonly IMvpDialogService dialogs;
    public ObservableCollection<FiscalDocumentData> Documents{get;}=[];
    public ObservableCollection<SaleData> Candidates{get;}=[];
    public ObservableCollection<FiscalReviewEvent> History{get;}=[];
    public IReadOnlyList<string> States{get;}=["TODOS","SIN_REVISAR","REVISADO","OBSERVADO"];
    [ObservableProperty]private string search="";
    [ObservableProperty]private string reviewState="TODOS";
    [ObservableProperty]private string candidateSearch="";
    [ObservableProperty]private FiscalDocumentData? selected;
    [ObservableProperty]private SaleData? selectedSale;
    [ObservableProperty]private FiscalDetail? detail;
    [ObservableProperty]private int page=1;
    [ObservableProperty]private int total;
    [ObservableProperty]private int candidatePage=1;
    [ObservableProperty]private int candidateTotal;
    [ObservableProperty]private int historyPage=1;
    [ObservableProperty]private int historyTotal;
    public override bool CanRead=>fiscal.CanRead;
    public bool CanCreate=>CanEdit&&fiscal.CanCreate&&SelectedSale is not null;
    public bool CanReview=>CanEdit&&fiscal.CanReview&&Detail is {Document.State:"PENDIENTE",DteCount:0};
    public string PageLabel=>$"{Total} solicitudes · Página {Page}";
    public string CandidatePageLabel=>$"{CandidateTotal} ventas sin solicitud activa · Página {CandidatePage}";
    public string HistoryPageLabel=>$"{HistoryTotal} revisiones · Página {HistoryPage}";
    public string DetailMessage=>Detail is null?"Selecciona una solicitud y consulta su ficha.":Detail.ReviewLabel+". "+(Detail.Issues.Count==0?"Comprobaciones internas disponibles; no valida esquema ni aceptación de Hacienda.":string.Join(" ",Detail.Issues));
    public FiscalViewModel(FiscalWorkspaceService fiscal,FiscalPendingService pending,IMvpDialogService dialogs,ISessionController session,MvpOperationService operations,ILogger<FiscalViewModel> logger):base(session,operations,logger){this.fiscal=fiscal;this.pending=pending;this.dialogs=dialogs;}
    partial void OnSelectedChanged(FiscalDocumentData? value){Detail=null;History.Clear();HistoryPage=1;HistoryTotal=0;OnPropertyChanged(nameof(HistoryPageLabel));}
    partial void OnSelectedSaleChanged(SaleData? value)=>OnPropertyChanged(nameof(CanCreate));
    partial void OnDetailChanged(FiscalDetail? value){OnPropertyChanged(nameof(CanReview));OnPropertyChanged(nameof(DetailMessage));}
    protected override void PermissionsChanged(){OnPropertyChanged(nameof(CanCreate));OnPropertyChanged(nameof(CanReview));}
    protected override void Reset(){Documents.Clear();Candidates.Clear();History.Clear();Selected=null;SelectedSale=null;Detail=null;Page=CandidatePage=HistoryPage=1;Total=CandidateTotal=HistoryTotal=0;Labels();}
    private void Labels(){foreach(var n in new[]{nameof(PageLabel),nameof(CandidatePageLabel),nameof(HistoryPageLabel)})OnPropertyChanged(n);}
    [RelayCommand]private Task ReloadAsync(){Page=1;return ReadAsync(LoadDocumentsAsync);}
    private async Task LoadDocumentsAsync(long revision)
    {
        var search=Search;var state=ReviewState;var page=Page;var result=await fiscal.SearchAsync(search,state=="TODOS"?null:state,new(page));
        if(!Current(revision)||search!=Search||state!=ReviewState||page!=Page)return;
        var id=Selected?.Id;Documents.Clear();foreach(var row in result.Items)Documents.Add(row);Total=result.Total;Selected=Documents.FirstOrDefault(x=>x.Id==id);Labels();
        StatusMessage=Total==0?"No hay solicitudes para este filtro.":"Solicitudes internas consultadas. La emisión a Hacienda todavía no está habilitada.";
    }
    [RelayCommand]private async Task NextAsync(){if(IsBusy||Page*30>=Total)return;Page++;await ReadAsync(LoadDocumentsAsync);}
    [RelayCommand]private async Task PreviousAsync(){if(IsBusy||Page==1)return;Page--;await ReadAsync(LoadDocumentsAsync);}
    [RelayCommand]private Task FindSalesAsync(){CandidatePage=1;return ReadAsync(LoadCandidatesAsync);}
    private async Task LoadCandidatesAsync(long revision)
    {
        var search=CandidateSearch;var page=CandidatePage;var result=await fiscal.CandidatesAsync(search,new(page));
        if(!Current(revision)||search!=CandidateSearch||page!=CandidatePage)return;
        Candidates.Clear();foreach(var sale in result.Items)Candidates.Add(sale);SelectedSale=null;CandidateTotal=result.Total;Labels();
        StatusMessage=CandidateTotal==0?"No hay ventas confirmadas sin solicitud activa para esta búsqueda.":"Selecciona una venta para preparar una solicitud interna FE o CCFE.";
    }
    [RelayCommand]private async Task NextCandidateAsync(){if(IsBusy||CandidatePage*30>=CandidateTotal)return;CandidatePage++;await ReadAsync(LoadCandidatesAsync);}
    [RelayCommand]private async Task PreviousCandidateAsync(){if(IsBusy||CandidatePage==1)return;CandidatePage--;await ReadAsync(LoadCandidatesAsync);}
    [RelayCommand]private Task OpenAsync()=>ReadAsync(async revision=>{if(Selected is {} selected)await LoadDetailAsync(selected.Id,revision);});
    private async Task LoadDetailAsync(long id,long revision)
    {
        Detail=null;History.Clear();HistoryTotal=0;Labels();
        var data=await fiscal.DetailAsync(id);if(!Current(revision)||Selected?.Id!=id)return;Detail=data;HistoryPage=1;await LoadHistoryAsync(revision);
    }
    private async Task LoadHistoryAsync(long revision)
    {
        if(Detail is not {} detail)return;var page=HistoryPage;var result=await fiscal.HistoryAsync(detail.Document.Id,new(page));
        if(!Current(revision)||Detail?.Document.Id!=detail.Document.Id||HistoryPage!=page)return;
        History.Clear();foreach(var row in result.Items)History.Add(row);HistoryTotal=result.Total;Labels();
    }
    [RelayCommand]private async Task NextHistoryAsync(){if(IsBusy||HistoryPage*30>=HistoryTotal)return;HistoryPage++;await ReadAsync(LoadHistoryAsync);}
    [RelayCommand]private async Task PreviousHistoryAsync(){if(IsBusy||HistoryPage==1)return;HistoryPage--;await ReadAsync(LoadHistoryAsync);}
    public sealed record FiscalChoice(string Code,string Name);
    private async Task ShowAsync(MvpFormModel form){using(form){IsEditing=true;try{await dialogs.ShowAsync(form);}finally{IsEditing=false;}}}
    [RelayCommand]private async Task CreateAsync()
    {
        if(!CanCreate||SelectedSale is not {} sale)return;
        FiscalChoice[] types=[new("FE","Solicitud de Factura Electrónica"),new("CCFE","Solicitud de Crédito Fiscal Electrónico")];
        await ShowAsync(new MvpFormModel("Preparar solicitud fiscal interna","Preparar solicitud",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new PendingFiscal(id,sale.Id,f.Choice<FiscalChoice>("type").Code,f.Text("reason"));
            await Operation.StartAsync(request,id,q=>pending.RegisterAsync(q),receipt=>ReadAsync(async revision=>{await LoadDocumentsAsync(revision);if(!Current(revision))return;Selected=Documents.FirstOrDefault(x=>x.Id==receipt.Id);if(Selected is not null)await LoadDetailAsync(receipt.Id,revision);await LoadCandidatesAsync(revision);}));
        }){Description=$"Venta {sale.Number} · USD {sale.Total:N2}. Crear esta solicitud no emite, firma ni envía un DTE ni modifica la venta."}.Select("type","Tipo de solicitud",types,types[0]).Input("reason","Motivo de preparación (sin claves ni credenciales)"));
    }
    [RelayCommand]private async Task ReviewAsync()
    {
        if(!CanReview||Detail is not {} detail)return;
        FiscalChoice[] outcomes=[new("REVISADO","Datos revisados internamente"),new("OBSERVADO","Datos con observaciones")];
        await ShowAsync(new MvpFormModel("Registrar revisión interna","Guardar revisión",Operation,async f=>
        {
            var id=Guid.NewGuid();var request=new ReviewFiscalInternal(id,detail.Document.Id,detail.Document.RowVersion.ToArray(),detail.ReviewToken,f.Choice<FiscalChoice>("outcome").Code,f.Text("reason"));
            await Operation.StartAsync(request,id,q=>fiscal.ReviewAsync(q),_=>ReadAsync(async revision=>{await LoadDocumentsAsync(revision);if(Current(revision)&&Selected?.Id==detail.Document.Id)await LoadDetailAsync(detail.Document.Id,revision);}));
        }){Description="Revisa los datos originales y actuales. Esta revisión es administrativa: no acredita cumplimiento fiscal ni aceptación de Hacienda. No escribas contraseñas, tokens o datos de certificados."}.Select("outcome","Resultado interno",outcomes,detail.Issues.Count==0?outcomes[0]:outcomes[1]).Input("reason","Observación operativa"));
    }
}
