using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Services;

public sealed class CommercialParametersService(ICommercialParametersRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanManage => Has("AJUSTES.EDITAR");
    public Task<CommercialParameters> ReadAsync(CancellationToken ct = default)
    {
        Read("AJUSTES.EDITAR");
        return repository.ReadParametersAsync(UserId, ct);
    }
    public Task<Result<MvpReceipt>> SaveAsync(ChangeCommercialParameters request, CancellationToken ct = default) => Write(CanManage, request,
        () => repository.ChangeParametersAsync(request with { JustificationVersion = request.JustificationVersion.ToArray(),
            AuthorizationVersion = request.AuthorizationVersion.ToArray(), Reason = request.Reason.Trim() }, UserId, WorkstationId, ct));
}
