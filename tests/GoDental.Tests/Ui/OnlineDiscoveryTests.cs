using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Localization;
using GoDental.Application.Online;
using GoDental.Application.Services;
using GoDental.Infrastructure.Online;
using GoDental.Wpf.Dialogs;
using GoDental.Wpf.ViewModels.Operations;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class OnlineDiscoveryTests
{
    [Fact]public void DepartmentMapMatchesCurrentCatalogAndFiltersRealCoordinates()
    {
        Assert.Equal(14,OnlineMapCatalog.Departments.Count);Assert.Equal(DirectoryCatalog.Departments.Select(d=>d.Name).Order(),OnlineMapCatalog.Departments.Select(d=>d.Name).Order());
        var san=OnlineMapCatalog.Departments.Single(d=>d.Name=="San Salvador");Assert.True(san.Contains(new(-89.19,13.7)));Assert.False(san.Contains(new(-87,15)));Assert.All(san.Polygons.SelectMany(p=>p),r=>Assert.True(r.Count>=4));
    }
    [Fact]public void GeometryPreservesHolesAndSeparateIslands()
    {
        IReadOnlyList<GeoPoint> Ring(double x,double y,double width)=>new GeoPoint[]{new(x,y),new(x+width,y),new(x+width,y+width),new(x,y+width),new(x,y)};
        var d=new OnlineMapDepartment("Prueba",1,new IReadOnlyList<IReadOnlyList<GeoPoint>>[]{new[]{Ring(0,0,10),Ring(2,2,2)},new[]{Ring(20,20,2)}},new(0,0),new(0,0,22,22));
        Assert.True(d.Contains(new(1,1)));Assert.False(d.Contains(new(3,3)));Assert.True(d.Contains(new(21,21)));Assert.False(d.Contains(new(15,15)));
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct){Calls++;return send(r);}}
    private static string Row(int id,double lon=-89.19,double lat=13.7)=>JsonSerializer.Serialize(new{type="node",id,lon,lat,tags=new Dictionary<string,string>{{"name","Negocio público"},{"amenity","dentist"},{"contact:phone","+50370000000"}}});
    private static string Response(params string[] rows)=>"{\"elements\":["+string.Join(',',rows)+"]}";
    private static HttpResponseMessage Json(string s)=>new(HttpStatusCode.OK){Content=new StringContent(s,Encoding.UTF8,"application/json")};
    [Fact]public async Task QueryUsesFixedHttpsAndOnlyBoundedPublicDentalData()
    {
        string? query=null;var handler=new Handler(async r=>{Assert.Equal(OverpassDiscoveryProvider.Endpoint,r.RequestUri!.AbsoluteUri);Assert.Equal(HttpMethod.Post,r.Method);Assert.Null(r.Headers.Authorization);Assert.NotEmpty(r.Headers.UserAgent);query=Uri.UnescapeDataString((await r.Content!.ReadAsStringAsync()).Replace('+',' '));return Json(Response(Row(1),Row(1),Row(2,-87,15),"null","{\"type\":\"node\",\"id\":\"malformed\"}"));});
        using var provider=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);var result=await provider.DiscoverAsync(OnlineMapCatalog.Departments.Single(d=>d.Name=="San Salvador"),default);
        Assert.Single(result.Items);Assert.Equal("https://www.openstreetmap.org/node/1",result.Items[0].SourceUrl);Assert.Null(result.Items[0].WhatsApp);Assert.Contains("timeout:20",query);Assert.Contains("out body center 101",query);Assert.DoesNotContain("out center tags",query);
    }
    [Fact]public async Task ProviderCapsResultsAndDoesNotRetryOrInventWhatsApp()
    {
        var handler=new Handler(_=>Task.FromResult(Json(Response(Enumerable.Range(1,102).Select(i=>Row(i)).ToArray()))));using var provider=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);
        var result=await provider.DiscoverAsync(OnlineMapCatalog.Departments.Single(d=>d.Name=="San Salvador"),default);Assert.Equal(100,result.Items.Count);Assert.True(result.Limited);Assert.All(result.Items,r=>Assert.Null(r.WhatsApp));
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.DiscoverAsync(OnlineMapCatalog.Departments[0],default));Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData(HttpStatusCode.TooManyRequests)][InlineData(HttpStatusCode.Redirect)][InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SourceFailureDoesNotFollowRedirectsOrAutomaticallyRetry(HttpStatusCode code)
    {
        var handler=new Handler(_=>Task.FromResult(new HttpResponseMessage(code){Content=new StringContent("No imprimir datos del proveedor")}));using var p=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);
        var error=await Assert.ThrowsAsync<ArgumentException>(()=>p.DiscoverAsync(OnlineMapCatalog.Departments[0],default));Assert.DoesNotContain("No imprimir",error.Message);Assert.Equal(1,handler.Calls);
        Assert.Contains("HTTP "+(int)code,error.Message);
    }
    [Fact]public async Task AlternativeUsesAllowlistedEndpointAndCannotBypassRetryAfter()
    {
        var clock=new Clock();var handler=new Handler(r=>{Assert.Equal(OverpassDiscoveryProvider.AlternativeEndpoint,r.RequestUri!.AbsoluteUri);var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new(TimeSpan.FromMinutes(3));return Task.FromResult(response);});
        using var provider=new OverpassDiscoveryProvider(new HttpClient(handler),clock);var area=OnlineMapCatalog.Departments[0];
        var error=await Assert.ThrowsAsync<ArgumentException>(()=>provider.DiscoverAsync(area,default,OnlineDiscoveryServer.Alternative));Assert.Contains("HTTP 429",error.Message);
        clock.Now=clock.Now.AddMinutes(2);error=await Assert.ThrowsAsync<ArgumentException>(()=>provider.DiscoverAsync(area,default,OnlineDiscoveryServer.Main));Assert.Contains("60 segundos",error.Message);Assert.Equal(1,handler.Calls);
        await Assert.ThrowsAsync<ArgumentException>(()=>provider.DiscoverAsync(area,default,(OnlineDiscoveryServer)999));Assert.Equal(1,handler.Calls);
    }
    [Theory][InlineData(HttpStatusCode.BadRequest,"rechazó")][InlineData(HttpStatusCode.Forbidden,"denegó")][InlineData(HttpStatusCode.GatewayTimeout,"tiempo")]
    public async Task ProviderDistinguishesHttpFailuresWithoutExposingBody(HttpStatusCode code,string expected)
    {
        var handler=new Handler(_=>Task.FromResult(new HttpResponseMessage(code){Content=new StringContent("private debug")}));using var provider=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);
        var error=await Assert.ThrowsAsync<ArgumentException>(()=>provider.DiscoverAsync(OnlineMapCatalog.Departments[0],default));Assert.Contains(expected,error.Message);Assert.Contains("HTTP "+(int)code,error.Message);Assert.DoesNotContain("private debug",error.Message);Assert.Equal(1,handler.Calls);
    }
    [Fact]public async Task NodeCoordinatesAndWayCenterArePreserved()
    {
        var way="{\"type\":\"way\",\"id\":42,\"center\":{\"lat\":13.7,\"lon\":-89.19},\"tags\":{\"name\":\"Negocio en edificio\",\"healthcare\":\"dentist\"}}";
        var handler=new Handler(_=>Task.FromResult(Json(Response(Row(1),way))));using var provider=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);var result=await provider.DiscoverAsync(OnlineMapCatalog.Departments.Single(d=>d.Name=="San Salvador"),default);
        Assert.Equal(2,result.Items.Count);Assert.Contains(result.Items,x=>x.SourceId=="node/1"&&x.Latitude==13.7);Assert.Contains(result.Items,x=>x.SourceId=="way/42"&&x.Longitude==-89.19);
    }
    [Fact]public async Task ServerChangeCancelsAndDiscardsPreviousResults()
    {
        var(vm,_,_,source,_)=Create();vm.MapDepartment="San Salvador";var pending=new TaskCompletionSource<OnlineDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);source.Pending=pending.Task;
        var task=vm.DiscoverCommand.ExecuteAsync(null);vm.DiscoveryServer=OnlineDiscoveryServer.Alternative;pending.SetResult(OnlineDiscoveryTestProvider.Data);await task;Assert.Empty(vm.Discovered);
        source.Pending=null;await vm.DiscoverCommand.ExecuteAsync(null);Assert.Equal(OnlineDiscoveryServer.Alternative,source.LastServer);Assert.Single(vm.Discovered);
    }
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,10,23,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
    [Fact]public async Task OversizedAndIncompleteResponsesAreRejected()
    {
        foreach(var body in new[]{new string('x',2*1024*1024+1),"{\"elements\":[],\"remark\":\"private debug\"}"})
        {
            var handler=new Handler(_=>Task.FromResult(Json(body)));using var p=new OverpassDiscoveryProvider(new HttpClient(handler),TimeProvider.System);var e=await Assert.ThrowsAsync<ArgumentException>(()=>p.DiscoverAsync(OnlineMapCatalog.Departments[0],default));Assert.DoesNotContain("private debug",e.Message);
        }
    }
    private static (OnlineViewModel Vm,UserSession Session,OnlineUiRepository Repo,OnlineDiscoveryTestProvider Source,Dialogs Dialogs) Create(bool manage=true)
    {
        var s=new UserSession();s.Begin(new(1,"test","Prueba",1,[],manage?["COMERCIAL.VER","CLIENTES.VER","COMERCIAL.PROSPECTAR"]:["COMERCIAL.VER","CLIENTES.VER"],false));var repo=new OnlineUiRepository();var source=new OnlineDiscoveryTestProvider();var dialogs=new Dialogs();
        return(new(new(repo,s,source),dialogs,s,new(new MvpUiRepository(),s),NullLogger<OnlineViewModel>.Instance),s,repo,source,dialogs);
    }
    [Fact]public async Task MapSelectionAndDiscoveryAreReadOnlyUntilHumanConfirms()
    {
        var(vm,_,repo,_,dialogs)=Create();Assert.Equal(14,vm.MapRegions.Count);vm.SelectMapDepartmentCommand.Execute("San Salvador");Assert.Single(vm.MapRegions,r=>r.Selected);await vm.DiscoverCommand.ExecuteAsync(null);Assert.Single(vm.Discovered);Assert.Empty(repo.Writes);
        vm.SelectedDiscovered=vm.Discovered.Single();OnlineDraftModel? draft=null;dialogs.Action=f=>{draft=Assert.IsType<OnlineDraftModel>(f.Body);return Task.CompletedTask;};await vm.ReviewDiscoveredCommand.ExecuteAsync(null);
        Assert.NotNull(draft);Assert.Null(draft.Data.WhatsApp);Assert.Equal("https://www.openstreetmap.org/node/123",draft.SourceUrl);Assert.Null(draft.Snapshot().Municipality);Assert.Empty(repo.Writes);
    }
    [Fact]public async Task LateDiscoveryCannotLeakAfterChangingAreaOrIdentity()
    {
        var(vm,s,_,source,_)=Create();vm.MapDepartment="San Salvador";var tcs=new TaskCompletionSource<OnlineDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);source.Pending=tcs.Task;var read=vm.DiscoverCommand.ExecuteAsync(null);vm.MapDepartment="La Libertad";tcs.SetResult(OnlineDiscoveryTestProvider.Data);await read;Assert.Empty(vm.Discovered);
        tcs=new(TaskCreationOptions.RunContinuationsAsynchronously);source.Pending=tcs.Task;read=vm.DiscoverCommand.ExecuteAsync(null);s.Clear();tcs.SetResult(OnlineDiscoveryTestProvider.Data);await read;Assert.Empty(vm.Discovered);Assert.Equal("",vm.MapDepartment);
    }
    [Fact]public async Task ViewingPermissionDoesNotGrantNetworkDiscovery()
    {var(vm,_,repo,source,_)=Create(false);vm.MapDepartment="San Salvador";await vm.DiscoverCommand.ExecuteAsync(null);Assert.Equal(0,source.Calls);Assert.Empty(repo.Writes);}
    [Fact]public async Task PermissionRevokedDuringNetworkCallDiscardsResults()
    {
        var(vm,_,repo,source,_)=Create();vm.MapDepartment="San Salvador";var tcs=new TaskCompletionSource<OnlineDiscoveryResult>(TaskCreationOptions.RunContinuationsAsynchronously);source.Pending=tcs.Task;var read=vm.DiscoverCommand.ExecuteAsync(null);repo.DiscoveryDenied=true;tcs.SetResult(OnlineDiscoveryTestProvider.Data);await read;Assert.Empty(vm.Discovered);Assert.Empty(repo.Writes);
    }
    private sealed class Dialogs:IMvpDialogService{public Func<MvpFormModel,Task>? Action;public async Task<bool> ShowAsync(MvpFormModel f){if(Action is not null)await Action(f);return false;}}
}
