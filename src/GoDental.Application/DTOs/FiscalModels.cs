namespace GoDental.Application.DTOs;
public sealed record ReviewFiscalInternal(Guid OperationId,long DocumentId,byte[] ExpectedVersion,string ReviewToken,string Outcome,string Reason);
public sealed class FiscalDocumentData
{
    public long Id {get;set;}
    public long SaleId {get;set;}
    public string SaleNumber {get;set;}="";
    public string Type {get;set;}="";
    public string State {get;set;}="";
    public DateTimeOffset Created {get;set;}
    public string ReviewState {get;set;}="SIN_REVISAR";
    public string? Observation {get;set;}
    public DateTimeOffset? Reviewed {get;set;}
    public string? Reviewer {get;set;}
    public string? ReviewedFingerprint {get;set;}
    public byte[] RowVersion {get;set;}=[];
    public DateTime LocalDate=>Created.ToOffset(TimeSpan.FromHours(-6)).DateTime;
}
public sealed class FiscalReceiverData
{
    public long? Id {get;set;}
    public string Name {get;set;}="";
    public string? Nit {get;set;}
    public string? Nrc {get;set;}
    public string? Dui {get;set;}
    public string? Address {get;set;}
    public bool Active {get;set;}
}
public sealed record FiscalDetail(FiscalDocumentData Document,SaleData Sale,FiscalReceiverData Receiver,IReadOnlyList<SaleLineData> Lines,int DteCount,string Fingerprint,string ReviewToken)
{
    public bool ReviewCurrent=>Document.ReviewedFingerprint==Fingerprint;
    public string ReviewLabel=>Document.ReviewState=="SIN_REVISAR"?"Sin revisión interna":ReviewCurrent?Document.ReviewState+" · datos vigentes":Document.ReviewState+" · datos cambiaron: revisar nuevamente";
    public IReadOnlyList<string> Issues
    {
        get
        {
            var issues=new List<string>();
            if(Document.State!="PENDIENTE")issues.Add("El documento ya no está pendiente.");
            if(Sale.State!="CONFIRMADA")issues.Add("La venta ya no está confirmada sin posventa.");
            if(Document.Type is not("FE" or "CCFE"))issues.Add("Tipo fuera del alcance de preparación interna.");
            if(DteCount>0)issues.Add("Existe un DTE asociado: requiere su integración fiscal verificada.");
            if(Document.Type=="CCFE"&&(!Receiver.Active||string.IsNullOrWhiteSpace(Receiver.Nit)||string.IsNullOrWhiteSpace(Receiver.Nrc)||string.IsNullOrWhiteSpace(Receiver.Address)))issues.Add("Completa cliente activo, NIT, NRC y dirección fiscal en Clientes.");
            return issues;
        }
    }
}
public sealed class FiscalReviewEvent
{
    public DateTimeOffset Date {get;set;}
    public DateTime LocalDate=>Date.ToOffset(TimeSpan.FromHours(-6)).DateTime;
    public string User {get;set;}="";
    public string Outcome {get;set;}="";
    public string Reason {get;set;}="";
}
