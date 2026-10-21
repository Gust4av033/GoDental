using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Persistence;

public interface ICommercialParametersRepository
{
    Task<CommercialParameters> ReadParametersAsync(int userId, CancellationToken ct);
    Task<Result<MvpReceipt>> ChangeParametersAsync(ChangeCommercialParameters request, int userId, int? workstationId, CancellationToken ct);
}
