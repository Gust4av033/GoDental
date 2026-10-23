using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Services;
public sealed class FiscalWorkspaceService(IFiscalWorkspaceRepository repository,IUserSession session):MvpServiceAccess(session)
{
    public bool CanRead=>Has("FISCAL.VER","VENTAS.VER");
    public bool CanCreate=>CanRead&&Has("FISCAL.CONFIGURAR");
    public bool CanReview=>CanRead&&Has("FISCAL.REVISAR_INTERNO");
    public Task<MvpPage<FiscalDocumentData>> SearchAsync(string search,string? state,MvpFilter f,CancellationToken ct=default)
    {Read("FISCAL.VER","VENTAS.VER");Page(f);Search(search);if(state is not(null or "SIN_REVISAR" or "REVISADO" or "OBSERVADO"))throw new ArgumentException("Estado de revisión desconocido.");return GuardAsync(()=>repository.FiscalDocumentsAsync(search.Trim(),state,f,UserId,ct));}
    public Task<MvpPage<SaleData>> CandidatesAsync(string search,MvpFilter f,CancellationToken ct=default)
    {Read("FISCAL.VER","VENTAS.VER");Page(f);Search(search);return GuardAsync(()=>repository.FiscalCandidatesAsync(search.Trim(),f,UserId,ct));}
    public Task<FiscalDetail?> DetailAsync(long id,CancellationToken ct=default){Read("FISCAL.VER","VENTAS.VER");Id(id);return GuardAsync(()=>repository.FiscalDetailAsync(id,UserId,ct));}
    public Task<MvpPage<FiscalReviewEvent>> HistoryAsync(long id,MvpFilter f,CancellationToken ct=default){Read("FISCAL.VER","VENTAS.VER");Id(id);Page(f);return GuardAsync(()=>repository.FiscalHistoryAsync(id,f,UserId,ct));}
    public Task<Result<MvpReceipt>> ReviewAsync(ReviewFiscalInternal r,CancellationToken ct=default)=>!CanReview?Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso de revisión interna.")):Validate(r) is {} error?Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error)):repository.ReviewFiscalAsync(Freeze(r),UserId,WorkstationId,ct);
    private async Task<T> GuardAsync<T>(Func<Task<T>> read)
    {
        var revision=CurrentSession is ISessionController s?s.Revision:0;
        var result=await read();
        if(!CanRead||CurrentSession is ISessionController current&&current.Revision!=revision)
            throw new UnauthorizedAccessException("La sesión cambió durante la consulta fiscal.");
        return result;
    }
    private static void Search(string s){if(s is null||s.Length>200)throw new ArgumentException("Revisa la búsqueda.");}
    public static string? Validate(ReviewFiscalInternal r)=>r.OperationId==Guid.Empty||r.DocumentId<=0||r.ExpectedVersion?.Length!=8||r.ReviewToken is not{Length:64}||!r.ReviewToken.All(Uri.IsHexDigit)||r.Outcome is not("REVISADO" or "OBSERVADO")||!Validators.MvpValidator.Reason(r.Reason)?"Revisa documento, versión, resultado y observación.":null;
    public static ReviewFiscalInternal Freeze(ReviewFiscalInternal r)=>r with{ExpectedVersion=r.ExpectedVersion.ToArray(),Reason=r.Reason.Trim()};
}
