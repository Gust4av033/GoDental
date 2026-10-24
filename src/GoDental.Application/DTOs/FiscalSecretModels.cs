using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace GoDental.Application.DTOs;

// Valores INTERNOS GoDental: no son códigos/catálogos oficiales de Hacienda.
public enum FiscalSecretEnvironment { Test = 1, Production = 2 }
public enum FiscalSecretPurpose { SigningMaterial = 1, SigningProtection = 2, ApiCredential = 3 }

public readonly record struct FiscalSecretScope(Guid IssuerId, FiscalSecretEnvironment Environment)
{
    public bool IsValid => IssuerId != Guid.Empty && Enum.IsDefined(Environment);
}

public readonly record struct FiscalSecretReference(FiscalSecretScope Scope, FiscalSecretPurpose Purpose, Guid Version)
{
    public bool IsValid => Scope.IsValid && Enum.IsDefined(Purpose) && Version != Guid.Empty;
}

// Nunca DTO de respuesta. No devuelve strings ni deja que JSON serialice el contenido.
public sealed class SecretBuffer : IDisposable
{
    private byte[]? _bytes;
    private SecretBuffer(byte[] owned) => _bytes = owned;
    public static SecretBuffer CopyFrom(ReadOnlySpan<byte> value) => new(value.ToArray());
    [JsonIgnore]
    public ReadOnlyMemory<byte> Memory => _bytes ?? throw new ObjectDisposedException(nameof(SecretBuffer));
    public void Dispose()
    {
        var bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
    public override string ToString() => "Material privado omitido";
}

public enum SecretStoreFailure { InvalidReference, Unavailable, AlreadyExists, Revoked, InvalidCandidate }

public sealed class SecretStoreException(SecretStoreFailure failure)
    : Exception("No se pudo completar la operación del almacén privado.")
{
    public SecretStoreFailure Failure { get; } = failure;
}
