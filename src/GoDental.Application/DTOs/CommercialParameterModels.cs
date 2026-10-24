namespace GoDental.Application.DTOs;

public sealed record CommercialParameters(decimal Justification, decimal Authorization, byte[] JustificationVersion, byte[] AuthorizationVersion);
public sealed record ChangeCommercialParameters(Guid OperationId, decimal Justification, decimal Authorization,
    byte[] JustificationVersion, byte[] AuthorizationVersion, string Reason);
