using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
namespace GoDental.Application.Services;
public sealed class UserManagementService(IUserRepository repository, IUserSession session, IPasswordHasher hasher)
{
    public bool CanRead => session.IsAuthenticated && session.Permissions.Contains("USUARIOS.VER");
    public bool CanManage => CanRead && session.Permissions.Contains("USUARIOS.GESTIONAR") && session.Permissions.Contains("SEGURIDAD.ROLES_PERMISOS");
    public async Task<Result<IReadOnlyList<UserSummary>>> ListAsync(CancellationToken ct = default) => !CanRead
        ? Result<IReadOnlyList<UserSummary>>.Failure("FORBIDDEN", "No tienes permiso para consultar usuarios.")
        : Result<IReadOnlyList<UserSummary>>.Success(await repository.ListAsync(ct));
    public async Task<IReadOnlyList<CatalogOption>> RolesAsync(CancellationToken ct = default) => CanManage ? await repository.RolesAsync(ct) : [];
    public async Task<Result> CreateAsync(string userName, string displayName, int role, string password, CancellationToken ct = default)
    {
        if (!CanManage) return Denied();
        userName = userName.Trim(); displayName = displayName.Trim();
        var error = IdentityValidator.ValidateUser(userName, displayName) ?? IdentityValidator.ValidatePassword(userName, password);
        if (error is not null) return Result.Failure("VALIDATION", error);
        if (role <= 0) return Result.Failure("VALIDATION", "Selecciona un rol.");
        var revision = (session as ISessionController)?.Revision;
        var hash = await Task.Run(() => hasher.Hash(password), ct);
        if (!CanManage || revision != (session as ISessionController)?.Revision) return Denied();
        return await repository.CreateAsync(userName, displayName, role, hash, (int)session.UserId!.Value, (int?)session.WorkstationId, ct);
    }
    public Task<Result> SetActiveAsync(UserSummary user, bool active, string reason, CancellationToken ct = default)
    {
        if (!CanManage) return Task.FromResult(Denied());
        if (ValidateChange(user, reason) is { } error) return Task.FromResult(Result.Failure("VALIDATION", error));
        return repository.SetActiveAsync(user, active, reason.Trim(), (int)session.UserId!.Value, (int?)session.WorkstationId, ct);
    }
    public async Task<Result> ResetPasswordAsync(UserSummary user, string password, string reason, CancellationToken ct = default)
    {
        if (!CanManage) return Denied();
        var error = ValidateChange(user, reason) ?? IdentityValidator.ValidatePassword(user.UserName, password);
        if (error is not null) return Result.Failure("VALIDATION", error);
        var revision = (session as ISessionController)?.Revision;
        var hash = await Task.Run(() => hasher.Hash(password), ct);
        if (!CanManage || revision != (session as ISessionController)?.Revision) return Denied();
        return await repository.ResetPasswordAsync(user, hash, reason.Trim(), (int)session.UserId!.Value, (int?)session.WorkstationId, ct);
    }
    private string? ValidateChange(UserSummary user, string reason)
    {
        if (user.Id == session.UserId) return "Para tu cuenta, usa cambiar contraseña; no puedes desactivarte ni restablecerte desde este listado.";
        if (user.RowVersion.Length != 8) return "Recarga los usuarios antes de continuar.";
        return reason.Trim().Length is < 1 or > 500 ? "Escribe un motivo de hasta 500 caracteres." : null;
    }
    private static Result Denied() => Result.Failure("FORBIDDEN", "No tienes permiso para administrar usuarios y roles.");
}
