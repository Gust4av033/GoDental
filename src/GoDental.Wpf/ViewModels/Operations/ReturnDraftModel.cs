using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Domain.Rules;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed record ReturnPhotoInput(string Name,byte[] Image);
public interface IReturnPhotoPicker{Task<IReadOnlyList<ReturnPhotoInput>> PickAsync();}
public sealed partial class ReturnDraftLine:ObservableObject
{
    public ReturnableSaleLine Source{get;}
    public string Caption=>$"{Source.Sku} · {Source.Name} · Disponible para devolver: {Source.Remaining:0.####}";
    public string[] Conditions{get;}=["SELLADO","ABIERTO_DEFECTUOSO","DAÑADO","OTRO"];
    public ObservableCollection<ReturnEvidence> Evidence{get;}=[];
    [ObservableProperty]private ReturnEvidence? selectedEvidence;
    [ObservableProperty]private string quantity="0";
    [ObservableProperty]private string condition="SELLADO";
    [ObservableProperty]private bool restock=true;
    [ObservableProperty]private string noRestockReason="";
    public bool Editable{get;}
    public ReturnDraftLine(ReturnableSaleLine source,bool cancel){Source=source;Editable=!cancel;Quantity=cancel?source.Remaining.ToString(System.Globalization.CultureInfo.CurrentCulture):"0";}
    partial void OnConditionChanged(string value){if(value!="SELLADO")Restock=false;}
}
public sealed partial class ReturnDraftModel:ObservableObject
{
    private readonly SaleReturnService service;
    private readonly IReturnPhotoPicker? picker;
    public ObservableCollection<ReturnDraftLine> Lines{get;}=[];
    [ObservableProperty]private ReturnDraftLine? selectedLine;
    [ObservableProperty]private string status="Selecciona una línea para adjuntar fotos. Las fotos se conservan centralmente; una solicitud no mueve stock ni caja.";
    [ObservableProperty]private bool isBusy;
    public ReturnDraftModel(SaleReturnsSnapshot source,bool cancel,SaleReturnService service,IReturnPhotoPicker? picker)
    {this.service=service;this.picker=picker;foreach(var line in source.Lines.Where(l=>l.Remaining>0))Lines.Add(new(line,cancel));SelectedLine=Lines.FirstOrDefault();}
    public IReadOnlyList<ReturnItem> Snapshot()
    {
        if(IsBusy)throw new ArgumentException("Espera a que terminen de adjuntarse las fotos.");
        return Lines.Select(l=>new ReturnItem(l.Source.Id,CheckoutModel.Parse(l.Quantity,"cantidad a devolver"),l.Condition,l.Restock,l.NoRestockReason,l.Evidence.ToArray())).Where(l=>l.Quantity!=0).ToArray();
    }
    [RelayCommand]private async Task AttachAsync()
    {
        if(IsBusy||picker is null||SelectedLine is not {} line)return;IsBusy=true;
        try
        {
            var photos=await picker.PickAsync();if(photos.Count+line.Evidence.Count>5)throw new ArgumentException("Adjunta hasta 5 fotos por línea.");
            foreach(var p in photos)line.Evidence.Add(await service.UploadPhotoAsync(p.Image,p.Name));
            Status="Fotos adjuntadas y verificadas. El administrador podrá consultarlas antes de autorizar.";
        }
        catch(ArgumentException ex){Status=ex.Message;}
        catch(Exception){Status="No se pudieron adjuntar las fotos. Revisa el almacenamiento central y vuelve a intentarlo.";}
        finally{IsBusy=false;}
    }
    [RelayCommand]private void RemovePhoto(){if(!IsBusy&&SelectedLine is {} line&&line.SelectedEvidence is {} e)line.Evidence.Remove(e);}
}
