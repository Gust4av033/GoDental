namespace GoDental.Application.DTOs;

public sealed record OnlineSegmentFilter(string? Department=null,string? Municipality=null,string? Type=null,string? State=null,string? Source=null,string Channel="WHATSAPP");
public sealed class OnlineZone
{
    public long Id {get;set;}
    public string Department {get;set;}="";
    public string? Municipality {get;set;}
    public short Priority {get;set;}=100;
    public bool Active {get;set;}=true;
    public int Prospects {get;set;}
    public byte[] RowVersion {get;set;}=[];
    public string Name=>$"{Department} · {Municipality??"Todo el departamento"}";
}
public sealed class OnlineSegment
{
    public long Id {get;set;}
    public string Name {get;set;}="";
    public string? Department {get;set;}
    public string? Municipality {get;set;}
    public string? Type {get;set;}
    public string? State {get;set;}
    public string? Source {get;set;}
    public string Channel {get;set;}="WHATSAPP";
    public bool Active {get;set;}=true;
    public byte[] RowVersion {get;set;}=[];
    public OnlineSegmentFilter Filter=>new(Department,Municipality,Type,State,Source,Channel);
    public string Criteria=>$"{Department??"Todo el país"} · {Municipality??"Todos los municipios"} · {Type??"Todos los negocios"} · {State??"Estados elegibles"} · {Source??"Todas las fuentes"} · {Channel}";
}
public sealed class OnlineTemplate
{
    public long Id {get;set;}
    public string Name {get;set;}="";
    public string Channel {get;set;}="WHATSAPP";
    public string Content {get;set;}="";
    public bool Active {get;set;}=true;
    public byte[] RowVersion {get;set;}=[];
    public string Preview=>Content.Replace("{nombre}","Negocio de ejemplo",StringComparison.Ordinal);
}
public sealed class OnlineCampaign
{
    public long Id {get;set;}
    public string Name {get;set;}="";
    public string Channel {get;set;}="";
    public string State {get;set;}="BORRADOR";
    public long? SegmentId {get;set;}
    public string? Segment {get;set;}
    public string? CriteriaJson {get;set;}
    public string? Content {get;set;}
    public int Recipients {get;set;}
    public int Suppressed {get;set;}
    public string? ApprovedBy {get;set;}
    public DateTimeOffset? ApprovedAt {get;set;}
    public byte[] RowVersion {get;set;}=[];
    public bool Internal=>CriteriaJson is not null&&Content is not null;
}
public sealed class OnlineRecipient
{
    public long Id {get;set;}
    public string Name {get;set;}="";
    public string? Contact {get;set;}
    public string State {get;set;}="";
    public bool Suppressed {get;set;}
    public string Status=>Suppressed?"Excluido o ficha modificada":"Preparado · consentimiento pendiente";
}
public sealed class OnlineFollowUp
{
    public long Id {get;set;}
    public long ProspectId {get;set;}
    public string Name {get;set;}="";
    public string Result {get;set;}="";
    public string Channel {get;set;}="";
    public DateTimeOffset Date {get;set;}
    public DateTime LocalDate=>Date.ToOffset(TimeSpan.FromHours(-6)).DateTime;
    public DateTime? NextDate {get;set;}
    public string Note {get;set;}="";
    public string User {get;set;}="";
}
public sealed record SaveOnlineZone(Guid OperationId,long Id,byte[] ExpectedVersion,string Department,string? Municipality,short Priority,bool Active,string Reason);
public sealed record SaveOnlineSegment(Guid OperationId,long Id,byte[] ExpectedVersion,string Name,OnlineSegmentFilter Filter,bool Active,string Reason);
public sealed record SaveOnlineTemplate(Guid OperationId,long Id,byte[] ExpectedVersion,string Name,string Channel,string Content,bool Active,string Reason);
public sealed record CreateOnlineCampaign(Guid OperationId,string Name,long SegmentId,byte[] SegmentVersion,long TemplateId,byte[] TemplateVersion,string Reason);
public sealed record TransitionOnlineCampaign(Guid OperationId,long Id,byte[] ExpectedVersion,string Action,string Reason);
public sealed record RecordOnlineFollowUp(Guid OperationId,long ProspectId,byte[] ExpectedVersion,string Result,string Channel,DateTime Date,DateTime? NextDate,string Note,string Reason);
