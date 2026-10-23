using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IFiscalWorkspaceRepository
{
    Task<MvpPage<FiscalDocumentData>> FiscalDocumentsAsync(string search,string? reviewState,MvpFilter filter,int userId,CancellationToken ct);
    Task<MvpPage<SaleData>> FiscalCandidatesAsync(string search,MvpFilter filter,int userId,CancellationToken ct);
    Task<FiscalDetail?> FiscalDetailAsync(long id,int userId,CancellationToken ct);
    Task<MvpPage<FiscalReviewEvent>> FiscalHistoryAsync(long id,MvpFilter filter,int userId,CancellationToken ct);
    Task<Result<MvpReceipt>> ReviewFiscalAsync(ReviewFiscalInternal request,int userId,int? station,CancellationToken ct);
}
