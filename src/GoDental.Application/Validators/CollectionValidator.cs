using GoDental.Application.DTOs;
namespace GoDental.Application.Validators;
public static class CollectionValidator
{
    public static IReadOnlyList<string> Media { get; } = Array.AsReadOnly(new[] { "INTERNO", "LLAMADA", "WHATSAPP", "EMAIL", "PRESENCIAL" });
    public static IReadOnlyList<string> Outcomes { get; } = Array.AsReadOnly(new[] { "GESTION_INTERNA", "CONTACTADO", "SIN_RESPUESTA" });
    public static string? Validate(RegisterCollectionNotice r) => r.OperationId == Guid.Empty || r.AccountId <= 0 || !MvpValidator.Version(r.ExpectedVersion)
        || !Media.Contains(r.Medium) || !Outcomes.Contains(r.Outcome) || !MvpValidator.Reason(r.Reason)
        ? "Revisa cuenta, versión, medio, resultado y observación de hasta 500 caracteres." : null;
    public static RegisterCollectionNotice Freeze(RegisterCollectionNotice r) => r with { ExpectedVersion = r.ExpectedVersion.ToArray(), Reason = r.Reason.Trim() };
}
