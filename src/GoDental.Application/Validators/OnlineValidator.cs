using System.Net;
using System.Net.Mail;
using GoDental.Application.DTOs;
using GoDental.Application.Localization;
namespace GoDental.Application.Validators;
public static class OnlineValidator
{
    public static readonly string[] States=["NUEVO","REVISADO","CONTACTADO","INTERESADO","CONVERTIDO","DESCARTADO"];
    public static readonly string[] SourceTypes=["WEB","GOOGLE","INSTAGRAM","FACEBOOK","OTRA"];
    public static readonly string[] Channels=["WHATSAPP","EMAIL","INSTAGRAM","FACEBOOK"];
    public static string? Validate(object request)=>request switch
    {
        SaveOnlineProspect r=>!Op(r.OperationId,r.Reason)||r.Id<0||r.ExpectedVersion is null||(r.Id==0?r.ExpectedVersion.Length!=0:r.ExpectedVersion.Length!=8)||r.Data is null?"Revisa identidad, versión y motivo.":Bounds(r.Data)??(r.Id==0?(r.Source is null?"Registra la fuente pública y fecha.":Source(r.Source)):r.Source is not null?"Añade fuentes con su operación específica.":null),
        AddOnlineSource r=>!Version(r.OperationId,r.ProspectId,r.ExpectedVersion,r.Reason)?"Actualiza la ficha y escribe motivo.":Source(r.Source),
        ChangeOnlineState r=>!Version(r.OperationId,r.ProspectId,r.ExpectedVersion,r.Reason)||r.State is not("REVISADO" or "DESCARTADO")?"Sólo se registran revisión/descarte internos.":null,
        BlockOnlineProspect r=>!Version(r.OperationId,r.ProspectId,r.ExpectedVersion,r.Reason)?"Actualiza la ficha y escribe motivo.":null,
        ExcludeOnlineContact r=>!Op(r.OperationId,r.Reason)||!Channels.Contains(r.Channel)||Identifier(r.Channel,r.Identifier) is null?"Revisa canal, identificador público y motivo.":null,
        ConvertOnlineProspect r=>!Version(r.OperationId,r.ProspectId,r.ExpectedVersion,r.Reason)||r.SaleId<=0||r.SaleVersion?.Length!=8?"Actualiza prospecto y venta confirmada.":null,
        _=>"Operación Online desconocida."
    };
    private static bool Op(Guid id,string reason)=>id!=Guid.Empty&&MvpValidator.Reason(reason);
    private static bool Version(Guid id,long entity,byte[]? version,string reason)=>Op(id,reason)&&entity>0&&version?.Length==8;
    public static string? Bounds(OnlineProspectInput d)=>string.IsNullOrWhiteSpace(d.Name)||d.Name.Trim().Length>200||d.Type?.Length>40||d.Phone?.Length>40||d.WhatsApp?.Length>40||d.Email?.Length>254||d.Website?.Length>1000||d.Instagram?.Length>200||d.Facebook?.Length>200||d.Department?.Length>100||d.Municipality?.Length>100||d.District?.Length>100?"Revisa nombre y longitudes de los datos públicos.":null;
    public static string? Changed(OnlineProspectInput d,OnlineProspectInput? old)
    {
        if(Bounds(d) is {} error)return error;
        if(PhoneFormat.Error(d.Phone,old?.Phone,"SV",old is null) is {} phone)return phone;
        if(PhoneFormat.Error(d.WhatsApp,old?.WhatsApp,"SV",old is null) is {} wa)return wa;
        if((old is null||d.Email!=old.Email)&&Text(d.Email) is {} email&&(!MailAddress.TryCreate(email,out var parsed)||parsed.Address!=email))return "Revisa el correo empresarial.";
        if(Text(d.Website) is {} site&&!PublicUrl(site,out _))return "Sitio empresarial: URL HTTPS pública sin credenciales, parámetros ni fragmentos.";
        foreach(var (value,channel,previous) in new[]{(d.Instagram,"INSTAGRAM",old?.Instagram),(d.Facebook,"FACEBOOK",old?.Facebook)})
            if((old is null||value!=previous)&&Text(value) is not null&&Identifier(channel,value!) is null)return "Usa la URL HTTPS pública del perfil social, sin parámetros.";
        if(old is null||d.Department!=old.Department||d.Municipality!=old.Municipality||d.District!=old.District)
            if(DirectoryCatalog.LocationError(d.Department,d.Municipality,d.District) is {} location)return location;
        if(old is null&&new[]{d.Phone,d.WhatsApp,d.Email,d.Website,d.Instagram,d.Facebook}.All(string.IsNullOrWhiteSpace)&&string.IsNullOrWhiteSpace(d.Municipality))return "Registra un contacto público, sitio empresarial o municipio para comparar coincidencias.";
        return null;
    }
    public static string? Source(OnlineSourceInput? s)=>s is null||!SourceTypes.Contains(s.Type)||!PublicUrl(s.Url,out _)||s.Url.Length>1000||s.Obtained.Year<2000||s.Obtained.Date>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date||s.Description?.Length>300?"Revisa tipo, URL HTTPS pública, fecha de obtención y descripción (300).":null;
    public static bool PublicUrl(string? text,out Uri? uri)
    {
        uri=null;if(string.IsNullOrWhiteSpace(text)||text.Any(char.IsControl)||!Uri.TryCreate(text.Trim(),UriKind.Absolute,out var u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo.Length>0||u.Query.Length>0||u.Fragment.Length>0||u.HostNameType!=UriHostNameType.Dns||u.IdnHost.TrimEnd('.').Length>253||!u.IdnHost.TrimEnd('.').Contains('.')||IPAddress.TryParse(u.IdnHost,out _))return false;
        var host=u.IdnHost.TrimEnd('.').ToLowerInvariant();if(new[]{".localhost",".local",".internal",".lan",".home",".test"}.Any(host.EndsWith)||u.IsLoopback)return false;uri=u;return true;
    }
    public static string? Domain(string? value)=>Text(value) is null?null:PublicUrl(value,out var uri)?uri!.IdnHost.TrimEnd('.').ToLowerInvariant().ReplacePrefix("www."):null;
    public static string? Identifier(string channel,string value)
    {
        var s=Text(value);if(s is null)return null;
        if(channel=="WHATSAPP")
        {
            if(!PhoneFormat.TryFormat(s,"SV",out _,out _))return null;
            var digits=new string(s.Where(char.IsAsciiDigit).ToArray());return "+"+(!s.StartsWith('+')&&digits.Length==8?"503":"")+digits;
        }
        if(channel=="EMAIL")return s.Length<=254&&MailAddress.TryCreate(s,out var m)&&m.Address==s?s.ToLowerInvariant():null;
        if(channel is not("INSTAGRAM" or "FACEBOOK")||s.Length>200||!PublicUrl(s,out var u))return null;
        var host=u!.IdnHost.ToLowerInvariant().ReplacePrefix("www.");if(channel=="FACEBOOK")host=host.ReplacePrefix("m.");
        return host!=channel.ToLowerInvariant()+".com"||u.AbsolutePath.Trim('/').Length==0?null:"https://"+host+Uri.UnescapeDataString(u.AbsolutePath).TrimEnd('/').ToLowerInvariant();
    }
    private static string ReplacePrefix(this string s,string prefix)=>s.StartsWith(prefix,StringComparison.Ordinal)?s[prefix.Length..]:s;
    public static string? Text(string? s)=>string.IsNullOrWhiteSpace(s)?null:s.Trim();
    public static OnlineProspectInput Copy(OnlineProspectInput d)=>new(){Name=d.Name,Type=d.Type,Phone=d.Phone,WhatsApp=d.WhatsApp,Email=d.Email,Website=d.Website,Instagram=d.Instagram,Facebook=d.Facebook,Department=d.Department,Municipality=d.Municipality,District=d.District};
    public static OnlineProspectInput Normalize(OnlineProspectInput d,OnlineProspectInput? old)
    {
        string? Clean(string? value,string? previous)=>old is not null&&value==previous?value:Text(value);
        return new(){Name=Clean(d.Name,old?.Name)!,Type=Clean(d.Type,old?.Type),Phone=PhoneFormat.NormalizeChanged(d.Phone,old?.Phone,"SV",old is null),WhatsApp=PhoneFormat.NormalizeChanged(d.WhatsApp,old?.WhatsApp,"SV",old is null),Email=Clean(d.Email,old?.Email),Website=Clean(d.Website,old?.Website),Instagram=old is not null&&d.Instagram==old.Instagram?d.Instagram:Text(d.Instagram) is null?null:Identifier("INSTAGRAM",d.Instagram!),Facebook=old is not null&&d.Facebook==old.Facebook?d.Facebook:Text(d.Facebook) is null?null:Identifier("FACEBOOK",d.Facebook!),Department=Clean(d.Department,old?.Department),Municipality=Clean(d.Municipality,old?.Municipality),District=Clean(d.District,old?.District)};
    }
    public static OnlineSourceInput FreezeSource(OnlineSourceInput s)=>s with{Url=s.Url.Trim(),Obtained=DateTime.SpecifyKind(s.Obtained.Date,DateTimeKind.Unspecified),Description=Text(s.Description)};
    public static object Freeze(object r)=>r switch
    {
        SaveOnlineProspect s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Data=Copy(s.Data),Source=s.Source is null?null:FreezeSource(s.Source),Reason=s.Reason.Trim()},
        AddOnlineSource s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Source=FreezeSource(s.Source),Reason=s.Reason.Trim()},
        ChangeOnlineState s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Reason=s.Reason.Trim()},
        BlockOnlineProspect s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Reason=s.Reason.Trim()},
        ExcludeOnlineContact s=>s with{Identifier=Identifier(s.Channel,s.Identifier)??s.Identifier.Trim(),Reason=s.Reason.Trim()},
        ConvertOnlineProspect s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),SaleVersion=s.SaleVersion.ToArray(),Reason=s.Reason.Trim()},
        _=>throw new ArgumentException("Operación desconocida.")
    };
    public static (Guid Id,string Type) Identity(object r)=>r switch
    {
        SaveOnlineProspect s=>(s.OperationId,"ONLINE_PROSPECTO"),AddOnlineSource s=>(s.OperationId,"ONLINE_FUENTE"),ChangeOnlineState s=>(s.OperationId,"ONLINE_ESTADO"),
        BlockOnlineProspect s=>(s.OperationId,"ONLINE_BLOQUEAR"),ExcludeOnlineContact s=>(s.OperationId,"ONLINE_EXCLUSION"),ConvertOnlineProspect s=>(s.OperationId,"ONLINE_CONVERTIR"),_=>throw new ArgumentException("Operación desconocida.")
    };
}
