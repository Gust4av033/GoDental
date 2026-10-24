using GoDental.Application.DTOs;
using GoDental.Application.Localization;
namespace GoDental.Application.Validators;
public static partial class OnlineValidator
{
    public static readonly string[] FollowUpResults=["CONTACTADO","RESPONDIO","INTERESADO","SIN_INTERES","NOTA"];
    public static string? FilterError(OnlineSegmentFilter? f)=>f is null||!Channels.Contains(f.Channel)||f.Type?.Length>40||f.State is not null&&(!States.Contains(f.State)||f.State is "CONVERTIDO" or "DESCARTADO")||f.Source is not null&&!SourceTypes.Contains(f.Source)?"Revisa filtros y canal del segmento.":DirectoryCatalog.LocationError(f.Department,f.Municipality,null);
    public static string? TemplateError(string? content)
    {
        if(string.IsNullOrWhiteSpace(content)||content.Length>4000||content.Any(c=>char.IsControl(c)&&c is not('\r' or '\n' or '\t')))return "Escribe un mensaje de texto de hasta 4000 caracteres.";
        var plain=content.Replace("{nombre}","",StringComparison.Ordinal);
        return plain.Contains('{')||plain.Contains('}')?"El único marcador admitido es {nombre}.":null;
    }
    private static bool Edit(Guid op,long id,byte[]? version,string reason)=>Op(op,reason)&&id>=0&&version is not null&&(id==0?version.Length==0:version.Length==8);
    private static bool Name(string? s,int length)=>!string.IsNullOrWhiteSpace(s)&&s.Trim().Length<=length;
    private static string? ValidatePlanning(object r)=>r switch
    {
        SaveOnlineZone s=>!Edit(s.OperationId,s.Id,s.ExpectedVersion,s.Reason)||s.Priority<0||!Name(s.Department,100)?"Revisa zona, prioridad, versión y motivo.":DirectoryCatalog.LocationError(s.Department,s.Municipality,null),
        SaveOnlineSegment s=>!Edit(s.OperationId,s.Id,s.ExpectedVersion,s.Reason)||!Name(s.Name,120)?"Revisa nombre, versión y motivo del segmento.":FilterError(s.Filter),
        SaveOnlineTemplate s=>!Edit(s.OperationId,s.Id,s.ExpectedVersion,s.Reason)||!Name(s.Name,120)||!Channels.Contains(s.Channel)?"Revisa plantilla, canal, versión y motivo.":TemplateError(s.Content),
        CreateOnlineCampaign s=>!Op(s.OperationId,s.Reason)||!Name(s.Name,150)||s.SegmentId<=0||s.TemplateId<=0||s.SegmentVersion?.Length!=8||s.TemplateVersion?.Length!=8?"Revisa nombre, segmento y plantilla actuales de la campaña.":null,
        TransitionOnlineCampaign s=>!Version(s.OperationId,s.Id,s.ExpectedVersion,s.Reason)||s.Action is not("PREPARAR" or "APROBAR" or "DEVOLVER" or "CANCELAR")?"Actualiza la campaña y elige una transición válida.":null,
        RecordOnlineFollowUp s=>!Version(s.OperationId,s.ProspectId,s.ExpectedVersion,s.Reason)||!FollowUpResults.Contains(s.Result)||!Channels.Contains(s.Channel)||!Name(s.Note,1500)||s.Date.Year<2000||s.Date.Date>DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date||s.NextDate is {} next&&(next.Date<s.Date.Date||next.Year>2100)?"Revisa resultado, canal, fecha, próxima gestión y nota (1500).":null,
        _=>"Operación Online desconocida."
    };
    private static object FreezePlanning(object r)=>r switch
    {
        SaveOnlineZone s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Department=s.Department.Trim(),Municipality=Text(s.Municipality),Reason=s.Reason.Trim()},
        SaveOnlineSegment s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Name=s.Name.Trim(),Filter=s.Filter with{Department=Text(s.Filter.Department),Municipality=Text(s.Filter.Municipality),Type=Text(s.Filter.Type)},Reason=s.Reason.Trim()},
        SaveOnlineTemplate s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Name=s.Name.Trim(),Content=s.Content.Trim(),Reason=s.Reason.Trim()},
        CreateOnlineCampaign s=>s with{Name=s.Name.Trim(),SegmentVersion=s.SegmentVersion.ToArray(),TemplateVersion=s.TemplateVersion.ToArray(),Reason=s.Reason.Trim()},
        TransitionOnlineCampaign s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Reason=s.Reason.Trim()},
        RecordOnlineFollowUp s=>s with{ExpectedVersion=s.ExpectedVersion.ToArray(),Date=DateTime.SpecifyKind(s.Date.Date,DateTimeKind.Unspecified),NextDate=s.NextDate is {} d?DateTime.SpecifyKind(d.Date,DateTimeKind.Unspecified):null,Note=s.Note.Trim(),Reason=s.Reason.Trim()},
        _=>throw new ArgumentException("Operación Online desconocida.")
    };
    private static (Guid Id,string Type) PlanningIdentity(object r)=>r switch
    {
        SaveOnlineZone s=>(s.OperationId,"ONLINE_ZONA"),SaveOnlineSegment s=>(s.OperationId,"ONLINE_SEGMENTO"),SaveOnlineTemplate s=>(s.OperationId,"ONLINE_PLANTILLA"),CreateOnlineCampaign s=>(s.OperationId,"ONLINE_CAMPANIA"),TransitionOnlineCampaign s=>(s.OperationId,"ONLINE_CAMPANIA_ESTADO"),RecordOnlineFollowUp s=>(s.OperationId,"ONLINE_SEGUIMIENTO"),_=>throw new ArgumentException("Operación Online desconocida.")
    };
}
