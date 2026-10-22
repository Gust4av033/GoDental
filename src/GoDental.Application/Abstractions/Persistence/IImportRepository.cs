using GoDental.Application.Common;
using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IImportRepository
{
    Task<ImportationSnapshot?> ReadImportAsync(long orderId, MvpFilter events, int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> WriteImportAsync(object request, int userId, int? workstationId, CancellationToken ct);
}
