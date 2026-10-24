using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface ISaleReturnRepository
{
    Task<ReturnEvidenceLocation> ReadEvidenceLocationAsync(int userId,CancellationToken ct);
    Task<Result<MvpReceipt>> ConfigureEvidenceAsync(ConfigureReturnEvidence request,int userId,int? workstationId,CancellationToken ct);
    Task<SaleReturnsSnapshot> ReadReturnsAsync(long saleId, MvpFilter filter, int userId, CancellationToken ct);
    Task<SaleReturnSnapshot?> ReadReturnAsync(long saleId, long returnId, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> RequestReturnAsync(RequestSaleReturn request, int userId, int? workstationId, CancellationToken ct);
    Task<Result<MvpReceipt>> ResolveReturnAsync(ResolveSaleReturn request, int administratorId, int userId, int? workstationId, CancellationToken ct);
}
