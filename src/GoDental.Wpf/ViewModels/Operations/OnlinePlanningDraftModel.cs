using CommunityToolkit.Mvvm.ComponentModel;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class OnlinePlanningDraftModel:ObservableObject
{
    public bool IsZone {get;init;}
    public bool IsSegment {get;init;}
    public bool IsTemplate {get;init;}
    public bool HasLocation=>IsZone||IsSegment;
    public bool HasChannel=>IsSegment||IsTemplate;
    public bool HasName=>IsSegment||IsTemplate;
    public AddressSelectionModel Location {get;init;}=new(new());
    public string[] Channels=>OnlineValidator.Channels;
    public string[] States {get;}=["TODOS","NUEVO","REVISADO","CONTACTADO","INTERESADO"];
    public string[] Sources {get;}=["TODAS",..OnlineValidator.SourceTypes];
    [ObservableProperty]private string name="";
    [ObservableProperty]private string channel="WHATSAPP";
    [ObservableProperty]private string type="";
    [ObservableProperty]private string state="TODOS";
    [ObservableProperty]private string source="TODAS";
    [ObservableProperty]private string content="Hola {nombre}, ";
    [ObservableProperty]private string priority="100";
    [ObservableProperty]private bool active=true;
    public string Preview=>Content.Replace("{nombre}","Negocio de ejemplo",StringComparison.Ordinal);
    partial void OnContentChanged(string value)=>OnPropertyChanged(nameof(Preview));
    public OnlineSegmentFilter Filter(){var l=Location.Apply(new());return new(l.Department,l.Municipality,OnlineValidator.Text(Type),State=="TODOS"?null:State,Source=="TODAS"?null:Source,Channel);}
    public short PriorityValue()=>short.TryParse(Priority,out var p)&&p>=0?p:throw new ArgumentException("Prioridad: número entero entre 0 y 32767.");
}
