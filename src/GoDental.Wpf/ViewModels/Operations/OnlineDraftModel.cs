using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class OnlineDraftModel:ObservableObject
{
    private readonly OnlineService service;
    public long Id {get;}
    public OnlineProspectInput Data {get;}
    public AddressSelectionModel Location {get;}
    public bool SourceOnly {get;}
    public bool ShowData=>!SourceOnly;
    public bool NeedsSource=>Id==0||SourceOnly;
    public string[] SourceTypes=>OnlineValidator.SourceTypes;
    [ObservableProperty]private string sourceType="WEB";
    [ObservableProperty]private string sourceUrl="";
    [ObservableProperty]private DateTime? obtained=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
    [ObservableProperty]private string description="";
    [ObservableProperty]private string matchMessage="Compara coincidencias antes de confirmar. La base vuelve a comprobarlas al guardar.";
    [ObservableProperty]private bool isBusy;
    private int query;
    public ObservableCollection<OnlineMatch> Matches{get;}=[];
    public OnlineDraftModel(OnlineService service,OnlineProspectData? data=null,bool sourceOnly=false){this.service=service;Id=data?.Id??0;Data=data is null?new():OnlineValidator.Copy(data);SourceOnly=sourceOnly;Location=new(new(){Department=Data.Department,Municipality=Data.Municipality,District=Data.District});}
    public OnlineProspectInput Snapshot(){var d=OnlineValidator.Copy(Data);var address=Location.Apply(new());d.Department=address.Department;d.Municipality=address.Municipality;d.District=address.District;return d;}
    public OnlineSourceInput Source()=>new(SourceType,SourceUrl,Obtained??throw new ArgumentException("Selecciona la fecha de obtención."),Description);
    [RelayCommand]private async Task CheckMatchesAsync()
    {
        if(IsBusy)return;IsBusy=true;var n=++query;var input=Snapshot();Matches.Clear();
        try{var result=await service.CheckMatchesAsync(input,Id,new());if(n!=query||Snapshot()!=input)return;foreach(var row in result.Items)Matches.Add(row);MatchMessage=$"{result.Total} coincidencias · se muestran las primeras {result.Items.Count}. Revisa el ID existente; no se fusiona ni contacta automáticamente.";}
        catch(UnauthorizedAccessException){MatchMessage="La sesión cambió o no tiene permiso.";}
        catch(ArgumentException ex){MatchMessage=ex.Message;}
        catch(Exception){MatchMessage="No se pudieron comparar coincidencias. Revisa la conexión.";}
        finally{IsBusy=false;}
    }
}
