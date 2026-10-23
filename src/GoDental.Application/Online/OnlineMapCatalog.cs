using System.Text.Json;
using GoDental.Application.Localization;
namespace GoDental.Application.Online;

public sealed record GeoPoint(double Longitude,double Latitude);
public sealed record GeoBounds(double South,double West,double North,double East);
public sealed record OnlineMapDepartment(string Name,long SourceId,IReadOnlyList<IReadOnlyList<IReadOnlyList<GeoPoint>>> Polygons,GeoPoint Label,GeoBounds Bounds)
{
    public bool Contains(GeoPoint point)=>Polygons.Any(p=>p.Aggregate(false,(inside,ring)=>inside^InsideRing(ring,point)));
    private static bool InsideRing(IReadOnlyList<GeoPoint> ring,GeoPoint p)
    {
        var inside=false;
        for(int i=0,j=ring.Count-1;i<ring.Count;j=i++)
        {
            var a=ring[i];var b=ring[j];
            if((a.Latitude>p.Latitude)!=(b.Latitude>p.Latitude)&&p.Longitude<(b.Longitude-a.Longitude)*(p.Latitude-a.Latitude)/(b.Latitude-a.Latitude)+a.Longitude)inside=!inside;
        }
        return inside;
    }
}
public static class OnlineMapCatalog
{
    public const string Attribution="Datos: Who’s On First / foursquare Quattroshapes · CC-BY 2.0";
    public const string OpenStreetMapLicenseUrl="https://www.openstreetmap.org/copyright";
    public const string GeometryLicenseUrl="https://github.com/foursquare/quattroshapes/";
    public const string LicenseUrl="https://github.com/whosonfirst-data/whosonfirst-data-admin-sv/blob/b8c0ae6f6469102c0dbf33fecdaab0209496ac9b/LICENSE.md";
    public static IReadOnlyList<OnlineMapDepartment> Departments {get;}=Load();
    private static IReadOnlyList<OnlineMapDepartment> Load()
    {
        using var stream=typeof(OnlineMapCatalog).Assembly.GetManifestResourceStream("GoDental.Application.Localization.Data.ElSalvador.Map.json")!;
        using var doc=JsonDocument.Parse(stream);
        var output=new List<OnlineMapDepartment>();
        foreach(var f in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var props=f.GetProperty("properties");var name=props.GetProperty("Name").GetString();
            var canonical=DirectoryCatalog.Departments.Single(x=>x.Name==name).Name;
            var geometry=f.GetProperty("geometry");var coordinates=geometry.GetProperty("coordinates");
            var polygons=new List<IReadOnlyList<IReadOnlyList<GeoPoint>>>();
            void Polygon(JsonElement poly)
            {
                var rings=poly.EnumerateArray().Select(r=>(IReadOnlyList<GeoPoint>)Array.AsReadOnly(r.EnumerateArray().Select(p=>new GeoPoint(p[0].GetDouble(),p[1].GetDouble())).ToArray())).ToArray();
                if(rings.Any(r=>r.Count<4||r.Any(p=>!double.IsFinite(p.Latitude)||!double.IsFinite(p.Longitude)||p.Latitude is <12 or >15||p.Longitude is <-91 or >-87)))throw new InvalidDataException("Geometría fuera del territorio esperado.");
                polygons.Add(Array.AsReadOnly(rings));
            }
            if(geometry.GetProperty("type").GetString()=="Polygon")Polygon(coordinates);
            else if(geometry.GetProperty("type").GetString()=="MultiPolygon")foreach(var poly in coordinates.EnumerateArray())Polygon(poly);
            else throw new InvalidDataException("Tipo de geometría no admitido.");
            var points=polygons.SelectMany(p=>p).SelectMany(r=>r).ToArray();
            var bounds=new GeoBounds(points.Min(p=>p.Latitude),points.Min(p=>p.Longitude),points.Max(p=>p.Latitude),points.Max(p=>p.Longitude));
            var label=new GeoPoint(props.TryGetProperty("LabelLongitude",out var x)?x.GetDouble():(bounds.East+bounds.West)/2,props.TryGetProperty("LabelLatitude",out var y)?y.GetDouble():(bounds.South+bounds.North)/2);
            output.Add(new(canonical,props.GetProperty("SourceId").GetInt64(),polygons.AsReadOnly(),label,bounds));
        }
        if(output.Count!=14||output.Select(d=>d.Name).Distinct().Count()!=14)throw new InvalidDataException("El mapa debe contener los 14 departamentos.");
        return output.AsReadOnly();
    }
}
