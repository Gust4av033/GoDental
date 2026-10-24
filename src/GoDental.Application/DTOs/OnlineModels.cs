namespace GoDental.Application.DTOs;
public record OnlineProspectInput
{
    public string Name {get;set;}="";
    public string? Type {get;set;}
    public string? Phone {get;set;}
    public string? WhatsApp {get;set;}
    public string? Email {get;set;}
    public string? Website {get;set;}
    public string? Instagram {get;set;}
    public string? Facebook {get;set;}
    public string? Department {get;set;}
    public string? Municipality {get;set;}
    public string? District {get;set;}
}
public sealed record OnlineProspectData:OnlineProspectInput
{
    public long Id {get;set;}
    public string State {get;set;}="NUEVO";
    public bool DoNotContact {get;set;}
    public bool Suppressed {get;set;}
    public long? CustomerId {get;set;}
    public string? Customer {get;set;}
    public long? ConversionSaleId {get;set;}
    public string? ConversionSale {get;set;}
    public DateTimeOffset? Created {get;set;}
    public byte[] RowVersion {get;set;}=[];
    public string SuppressionLabel=>Suppressed?"No contactar":"Sin exclusión registrada · no acredita consentimiento";
}
public sealed record OnlineSourceInput(string Type,string Url,DateTime Obtained,string? Description);
public sealed record SaveOnlineProspect(Guid OperationId,long Id,byte[] ExpectedVersion,OnlineProspectInput Data,OnlineSourceInput? Source,string Reason);
public sealed record AddOnlineSource(Guid OperationId,long ProspectId,byte[] ExpectedVersion,OnlineSourceInput Source,string Reason);
public sealed record ChangeOnlineState(Guid OperationId,long ProspectId,byte[] ExpectedVersion,string State,string Reason);
public sealed record BlockOnlineProspect(Guid OperationId,long ProspectId,byte[] ExpectedVersion,string Reason);
public sealed record ExcludeOnlineContact(Guid OperationId,string Channel,string Identifier,string Reason);
public sealed record ConvertOnlineProspect(Guid OperationId,long ProspectId,byte[] ExpectedVersion,long SaleId,byte[] SaleVersion,string Reason);
public sealed class OnlineSourceData
{
    public long Id {get;set;}
    public string Type {get;set;}="";
    public string Url {get;set;}="";
    public DateTimeOffset Obtained {get;set;}
    public DateTime LocalDate=>Obtained.ToOffset(TimeSpan.FromHours(-6)).DateTime;
    public string? Description {get;set;}
}
public sealed class OnlineMatch
{
    public string Entity {get;set;}="";
    public long Id {get;set;}
    public string Name {get;set;}="";
    public string State {get;set;}="";
    public string Match {get;set;}="";
}
public sealed class OnlineExclusion
{
    public long Id {get;set;}
    public string Channel {get;set;}="";
    public string Identifier {get;set;}="";
    public string? Reason {get;set;}
    public bool Active {get;set;}
    public DateTimeOffset Date {get;set;}
    public DateTime LocalDate=>Date.ToOffset(TimeSpan.FromHours(-6)).DateTime;
    public string? User {get;set;}
}
public sealed class OnlineEvent
{
    public DateTimeOffset Date {get;set;}
    public DateTime LocalDate=>Date.ToOffset(TimeSpan.FromHours(-6)).DateTime;
    public string User {get;set;}="";
    public string Action {get;set;}="";
    public string Reason {get;set;}="";
}
