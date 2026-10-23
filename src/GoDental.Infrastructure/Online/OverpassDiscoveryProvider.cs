using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GoDental.Application.Abstractions.Online;
using GoDental.Application.DTOs;
using GoDental.Application.Online;
namespace GoDental.Infrastructure.Online;

public sealed class OverpassDiscoveryProvider: IOnlineDiscoveryProvider,IDisposable
{
    public const string Endpoint="https://overpass-api.de/api/interpreter";
    public const string Attribution="© OpenStreetMap contributors · ODbL 1.0 · https://www.openstreetmap.org/copyright";
    private const int MaxBytes=2*1024*1024;
    private readonly HttpClient client;
    private readonly TimeProvider time;
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset last=DateTimeOffset.MinValue;
    public OverpassDiscoveryProvider():this(new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,Credentials=null,MaxResponseHeadersLength=16,ConnectTimeout=TimeSpan.FromSeconds(10)}){Timeout=Timeout.InfiniteTimeSpan},TimeProvider.System){}
    public OverpassDiscoveryProvider(HttpClient client,TimeProvider time){this.client=client;this.time=time;}
    public async Task<OnlineDiscoveryResult> DiscoverAsync(OnlineMapDepartment department,CancellationToken ct)
    {
        if(!OnlineMapCatalog.Departments.Contains(department))throw new ArgumentException("Usa una región del catálogo cartográfico.");
        if(!await gate.WaitAsync(0,ct))throw new ArgumentException("Ya hay una búsqueda en curso.");
        try
        {
            if(time.GetUtcNow()-last<TimeSpan.FromMinutes(1))throw new ArgumentException("Espera un minuto entre consultas a la fuente comunitaria.");
            last=time.GetUtcNow();
            var b=department.Bounds;
            var bbox=string.Join(",",new[]{b.South,b.West,b.North,b.East}.Select(n=>n.ToString("F6",CultureInfo.InvariantCulture)));
            var query=$"[out:json][timeout:20];(nwr[\"amenity\"=\"dentist\"]({bbox});nwr[\"healthcare\"=\"dentist\"]({bbox}););out center tags 101;";
            using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"data",query}})};
            request.Headers.UserAgent.ParseAdd("GoDental/0.1 (+https://github.com/Gust4av033/GoDental)");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(35));
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(response.StatusCode==HttpStatusCode.TooManyRequests)throw new ArgumentException("La fuente limitó las consultas. Inténtalo más tarde; no se reenvía automáticamente.");
            if(!response.IsSuccessStatusCode)throw new ArgumentException("La fuente comunitaria no está disponible. No se guardaron prospectos.");
            if(response.Content.Headers.ContentLength is >MaxBytes)throw new ArgumentException("La respuesta supera el límite de búsqueda.");
            await using var body=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();var bytes=new byte[8192];int count;
            while((count=await body.ReadAsync(bytes,timeout.Token))>0){if(buffer.Length+count>MaxBytes)throw new ArgumentException("La respuesta supera el límite de búsqueda.");await buffer.WriteAsync(bytes.AsMemory(0,count),timeout.Token);}
            return Parse(buffer.ToArray(),department,time.GetUtcNow());
        }
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new ArgumentException("La búsqueda agotó el tiempo de espera. No se guardaron prospectos.");}
        catch(HttpRequestException){throw new ArgumentException("No se pudo consultar la fuente por HTTPS. Revisa la conexión.");}
        catch(JsonException){throw new ArgumentException("La fuente devolvió una respuesta no válida. No se guardaron prospectos.");}
        finally{gate.Release();}
    }
    private static OnlineDiscoveryResult Parse(byte[] json,OnlineMapDepartment area,DateTimeOffset obtained)
    {
        using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=24});
        var root=doc.RootElement;
        if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("elements",out var elements)||elements.ValueKind!=JsonValueKind.Array)throw new JsonException();
        if(root.TryGetProperty("remark",out _))throw new ArgumentException("La fuente informó una consulta incompleta. Inténtalo más tarde.");
        var result=new List<OnlineDiscoveredBusiness>();var seen=new HashSet<string>();
        foreach(var e in elements.EnumerateArray().Take(101))
        {
            if(e.ValueKind!=JsonValueKind.Object||!e.TryGetProperty("type",out var t)||t.ValueKind!=JsonValueKind.String||t.GetString() is not("node" or "way" or "relation")||!e.TryGetProperty("id",out var id)||id.ValueKind!=JsonValueKind.Number||!id.TryGetInt64(out var number)||number<=0||!e.TryGetProperty("tags",out var tags)||tags.ValueKind!=JsonValueKind.Object)continue;
            if(Text(tags,"amenity",80)!="dentist"&&Text(tags,"healthcare",80)!="dentist")continue;
            var name=Text(tags,"name:es",200)??Text(tags,"name",200);if(name is null)continue;
            var point=t.GetString()=="node"?e:e.TryGetProperty("center",out var center)?center:default;
            if(point.ValueKind!=JsonValueKind.Object||!point.TryGetProperty("lon",out var lon)||!point.TryGetProperty("lat",out var lat)||lon.ValueKind!=JsonValueKind.Number||lat.ValueKind!=JsonValueKind.Number||!lon.TryGetDouble(out var x)||!lat.TryGetDouble(out var y)||!double.IsFinite(x)||!double.IsFinite(y)||!area.Contains(new(x,y)))continue;
            var key=t.GetString()+"/"+number.ToString(CultureInfo.InvariantCulture);if(!seen.Add(key))continue;
            var address=string.Join(", ",new[]{Text(tags,"addr:street",100),Text(tags,"addr:housenumber",30),Text(tags,"addr:city",100)}.Where(s=>s is not null));
            result.Add(new(key,name,"https://www.openstreetmap.org/"+key,x,y,Text(tags,"contact:phone",100)??Text(tags,"phone",100),Text(tags,"contact:whatsapp",100),Text(tags,"contact:email",254)??Text(tags,"email",254),Text(tags,"contact:website",1000)??Text(tags,"website",1000),address.Length==0?null:address));
            if(result.Count==100)break;
        }
        return new(result.AsReadOnly(),obtained,elements.GetArrayLength()>100,Attribution);
    }
    private static string? Text(JsonElement tags,string key,int limit)=>tags.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString() is {} s&&!string.IsNullOrWhiteSpace(s)&&s.Length<=limit&&!s.Any(char.IsControl)?s.Trim():null;
    // No se dispone el semáforo mientras una cancelación de HttpClient puede estar liberándolo.
    public void Dispose()=>client.Dispose();
}
