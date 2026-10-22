using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class ImportService(IImportRepository repository, IUserSession session) : MvpServiceAccess(session)
{
    public bool CanManage => Has("COMPRAS.VER", "COMPRAS.GESTIONAR");
    public Task<ImportationSnapshot?> ReadAsync(long orderId, CancellationToken ct=default, int eventPage=1) { Read("COMPRAS.VER"); Id(orderId); var filter = new MvpFilter(eventPage); Page(filter); return repository.ReadImportAsync(orderId,filter,UserId,ct); }
    public Task<Result<MvpReceipt>> SaveAsync(object request, CancellationToken ct=default) => !CanManage ? Task.FromResult(Result<MvpReceipt>.Failure("FORBIDDEN","No tienes permiso para gestionar importaciones."))
        : ImportValidator.Validate(request) is {} error ? Task.FromResult(Result<MvpReceipt>.Failure("VALIDATION",error)) : repository.WriteImportAsync(ImportValidator.Freeze(request),UserId,WorkstationId,ct);
}
