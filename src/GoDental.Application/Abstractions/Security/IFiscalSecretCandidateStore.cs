using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Security;

// Candidatos opacos SIN validación/aprobación fiscal. No registrar este contrato en WPF ni HTTP.
public interface IFiscalSecretCandidateStore
{
    Task CreateAsync(FiscalSecretReference reference, ReadOnlyMemory<byte> candidate, CancellationToken ct = default);
    Task<SecretBuffer> OpenAsync(FiscalSecretReference reference, CancellationToken ct = default);
    Task RevokeAsync(FiscalSecretReference reference, CancellationToken ct = default);
}
