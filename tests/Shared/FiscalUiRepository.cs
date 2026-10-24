using System.IO;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
// Datos sintéticos. No emite ni transmite documentos.
internal sealed class FiscalUiRepository : IFiscalWorkspaceRepository, IFiscalPendingRepository
{
    public List<ReviewFiscalInternal> Writes {get;}=[];
    public bool LoseResponse {get;set;}
    public Task<FiscalDetail?>? PendingDetail {get;set;}
    public Task<MvpPage<FiscalDocumentData>>? PendingSearch {get;set;}
    public FiscalDetail Detail {get;}=new(new(){Id=1,SaleId=1,SaleNumber="PRUEBA-001",Type="FE",State="PENDIENTE",RowVersion=new byte[8]},new(){Id=1,Number="PRUEBA-001",State="CONFIRMADA",Total=10},new(){Name="Consumidor final"},[],0,new string('A',64),new string('B',64));
    public Task<MvpPage<FiscalDocumentData>> FiscalDocumentsAsync(string s,string? state,MvpFilter f,int u,CancellationToken ct)=>PendingSearch??Task.FromResult(new MvpPage<FiscalDocumentData>([Detail.Document],1));
    public Task<MvpPage<SaleData>> FiscalCandidatesAsync(string s,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<SaleData>([Detail.Sale],1));
    public Task<FiscalDetail?> FiscalDetailAsync(long id,int u,CancellationToken ct)=>PendingDetail??Task.FromResult<FiscalDetail?>(Detail);
    public Task<MvpPage<FiscalReviewEvent>> FiscalHistoryAsync(long id,MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<FiscalReviewEvent>([new(){User="Revisor de prueba",Date=DateTimeOffset.UtcNow,Outcome="OBSERVADO",Reason="Datos sintéticos"}],1));
    public Task<Result<MvpReceipt>> ReviewFiscalAsync(ReviewFiscalInternal r,int u,int? station,CancellationToken ct){Writes.Add(r);if(LoseResponse)throw new IOException("Respuesta perdida en prueba");return Task.FromResult(Result<MvpReceipt>.Success(new(r.DocumentId,r.OperationId)));}
    public Task<Result<MvpReceipt>> RegisterPendingAsync(PendingFiscal r,int u,int? station,CancellationToken ct)=>Task.FromResult(Result<MvpReceipt>.Success(new(1,r.OperationId)));
    public Task<MvpPage<PendingFiscalData>> PendingAsync(MvpFilter f,int u,CancellationToken ct)=>Task.FromResult(new MvpPage<PendingFiscalData>([],0));
}
