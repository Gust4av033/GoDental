using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Localization;
using GoDental.Application.Online;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class OnlineMapRegion:ObservableObject
{
    public string Name {get;init;}="";
    public string PathData {get;init;}="";
    public double LabelX {get;init;}
    public double LabelY {get;init;}
    [ObservableProperty]private bool selected;
}
public sealed partial class OnlineViewModel
{
    public ObservableCollection<OnlineMapRegion> MapRegions {get;}=BuildMap();
    public IReadOnlyList<string> MapDepartments {get;}=DirectoryCatalog.Departments.Select(d=>d.Name).ToArray();
    public ObservableCollection<OnlineDiscoveredBusiness> Discovered {get;}=[];
    [ObservableProperty]private string mapDepartment="";
    [ObservableProperty]private string mapMessage="Elige un departamento en el mapa o en la lista. El mapa funciona sin conexión.";
    [ObservableProperty]private double mapZoom=1;
    [ObservableProperty]private OnlineDiscoveredBusiness? selectedDiscovered;
    private DateTimeOffset? discoveryDate;
    private string? discoveryDepartment;
    private CancellationTokenSource? currentDiscovery;
    public double MapWidth=>600*MapZoom;
    public double MapHeight=>330*MapZoom;
    public string MapAttribution=>OnlineMapCatalog.Attribution;
    public string DiscoveryAttribution=>"© OpenStreetMap contributors · ODbL 1.0 · www.openstreetmap.org/copyright";
    partial void OnSelectedZoneChanged(OnlineZone? value){if(value is not null&&MapDepartments.Contains(value.Department))MapDepartment=value.Department;}
    partial void OnMapZoomChanged(double value){OnPropertyChanged(nameof(MapWidth));OnPropertyChanged(nameof(MapHeight));}
    partial void OnMapDepartmentChanged(string value)
    {
        currentDiscovery?.Cancel();Discovered.Clear();SelectedDiscovered=null;discoveryDate=null;discoveryDepartment=null;
        foreach(var region in MapRegions)region.Selected=region.Name==value;
        MapMessage=string.IsNullOrEmpty(value)?"Elige un departamento para consultar su territorio.":$"{value} · límites departamentales de referencia. Verifica el municipio al registrar un negocio.";
    }
    [RelayCommand]private void SelectMapDepartment(string? name){if(name is not null&&MapDepartments.Contains(name))MapDepartment=name;}
    [RelayCommand]private void ZoomMapIn()=>MapZoom=Math.Min(4,MapZoom*1.25);
    [RelayCommand]private void ZoomMapOut()=>MapZoom=Math.Max(1,MapZoom/1.25);
    [RelayCommand]private void ResetMapZoom()=>MapZoom=1;
    [RelayCommand(IncludeCancelCommand=true)]private Task DiscoverAsync(CancellationToken ct)=>!CanManage?Task.CompletedTask:ReadAsync(async revision=>
    {
        var area=MapDepartment;if(!MapDepartments.Contains(area)){MapMessage="Elige primero un departamento.";return;}
        Discovered.Clear();SelectedDiscovered=null;discoveryDate=null;discoveryDepartment=null;
        using var cancel=CancellationTokenSource.CreateLinkedTokenSource(ct);currentDiscovery=cancel;MapMessage="Consultando negocios públicos de OpenStreetMap…";
        try
        {
            var result=await service.DiscoverAsync(area,cancel.Token);
            if(!Current(revision)||MapDepartment!=area||cancel.IsCancellationRequested)return;
            foreach(var item in result.Items)Discovered.Add(item);discoveryDate=result.Obtained;discoveryDepartment=area;
            MapMessage=$"{result.Items.Count} negocios encontrados en {area}. "+(result.Limited?"La fuente alcanzó su límite; la cobertura puede estar incompleta. ":"")+"Selecciona uno y revisa sus datos antes de guardarlo.";
        }
        catch(OperationCanceledException)when(cancel.IsCancellationRequested){if(Current(revision)&&MapDepartment==area)MapMessage="Búsqueda cancelada. No se guardaron prospectos.";}
        catch(ArgumentException ex){if(Current(revision)&&MapDepartment==area)MapMessage=ex.Message;}
        catch(UnauthorizedAccessException){if(Current(revision)&&MapDepartment==area)MapMessage="Tu sesión cambió o no tiene permiso para consultar la fuente.";}
        catch(Exception){if(Current(revision)&&MapDepartment==area)MapMessage="No se pudo consultar la fuente. Revisa la conexión e inténtalo más tarde.";}
        finally{if(ReferenceEquals(currentDiscovery,cancel))currentDiscovery=null;}
    });
    [RelayCommand]private Task ReviewDiscoveredAsync()
    {
        if(!CanManage||SelectedDiscovered is not {} b||discoveryDate is not {} date||discoveryDepartment!=MapDepartment)return Task.CompletedTask;
        var draft=new OnlineDraftModel(service){SourceType="WEB",SourceUrl=b.SourceUrl,Obtained=date.ToOffset(TimeSpan.FromHours(-6)).Date,Description="Negocio publicado en OpenStreetMap. © OpenStreetMap contributors, ODbL 1.0; datos revisados antes del alta."};
        draft.Data.Name=b.Name;draft.Data.Type="Negocio dental";draft.Location.Department=MapDepartment;
        if(b.Phone is {} phone&&PhoneFormat.TryFormat(phone,"SV",out var formatted,out _))draft.Data.Phone=formatted;
        if(b.WhatsApp is {} wa&&OnlineValidator.Identifier("WHATSAPP",wa)is {} whatsapp)draft.Data.WhatsApp=whatsapp;
        if(b.Email is {} email&&MailAddress.TryCreate(email,out var parsed)&&parsed.Address==email)draft.Data.Email=email;
        if(b.Website is {} site&&OnlineValidator.PublicUrl(site,out _))draft.Data.Website=site;
        return Show(new MvpFormModel("Revisar negocio encontrado","Guardar prospecto",Operation,f=>Commit(new SaveOnlineProspect(Guid.NewGuid(),0,[],draft.Snapshot(),draft.Source(),f.Text("reason")))){Body=draft,Description="Verifica nombre, contacto y municipio. Se conservan fuente y fecha; guardar comprueba coincidencias. Un dato publicado no acredita autorización comercial."}.Input("reason","Motivo / revisión del origen"));
    }
    private void ResetMap(){currentDiscovery?.Cancel();MapDepartment="";Discovered.Clear();SelectedDiscovered=null;discoveryDate=null;discoveryDepartment=null;MapZoom=1;}
    private static ObservableCollection<OnlineMapRegion> BuildMap()
    {
        var areas=OnlineMapCatalog.Departments;var west=areas.Min(d=>d.Bounds.West);var north=areas.Max(d=>d.Bounds.North);var east=areas.Max(d=>d.Bounds.East);var south=areas.Min(d=>d.Bounds.South);
        var cos=Math.Cos((north+south)/2*Math.PI/180);var factor=Math.Min(776/((east-west)*cos),416/(north-south));
        var ox=(800-(east-west)*cos*factor)/2;var oy=(440-(north-south)*factor)/2;
        (double X,double Y) Project(GeoPoint p)=>(ox+(p.Longitude-west)*cos*factor,oy+(north-p.Latitude)*factor);
        var regions=new ObservableCollection<OnlineMapRegion>();
        foreach(var area in areas)
        {
            var path=new StringBuilder("F0 ");
            foreach(var ring in area.Polygons.SelectMany(p=>p))
            {
                for(var i=0;i<ring.Count;i++){var p=Project(ring[i]);path.Append(i==0?'M':'L').Append(p.X.ToString("F3",CultureInfo.InvariantCulture)).Append(',').Append(p.Y.ToString("F3",CultureInfo.InvariantCulture)).Append(' ');}path.Append("Z ");
            }
            var label=Project(area.Label);regions.Add(new(){Name=area.Name,PathData=path.ToString(),LabelX=Math.Clamp(label.X-38,8,714),LabelY=Math.Clamp(label.Y-7,6,420)});
        }
        return regions;
    }
}
