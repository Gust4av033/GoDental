using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Security;

// Backend local: no aprobación fiscal, alias activos, parsing de certificado ni transporte.
[SupportedOSPlatform("windows")]
public sealed class WindowsFiscalSecretCandidateStore : IFiscalSecretCandidateStore
{
    public const int MaximumCandidateBytes = 64 * 1024;
    private const int MaximumCipherBytes = MaximumCandidateBytes + 8192;
    private const string ScopeFile = "scope.bin";
    private readonly string _root;
    private readonly FiscalSecretScope _scope;
    private readonly string _serviceSid;

    public WindowsFiscalSecretCandidateStore(string root, FiscalSecretScope scope, string serviceSid)
    {
        try
        {
            if (!scope.IsValid) throw new SecretStoreException(SecretStoreFailure.InvalidReference);
            _root = WindowsSecretDirectory.Normalize(root);
            _scope = scope;
            _serviceSid = serviceSid;
            ValidateRoot();
        }
        catch (Exception ex) when (IsProviderError(ex)) { throw Unavailable(); }
    }

    // Sólo provisión local explícita bajo la cuenta de servicio. Nunca cambia ACL existentes.
    public static void InitializeNew(string root, FiscalSecretScope scope, string serviceSid)
    {
        try
        {
            if (!scope.IsValid) throw new SecretStoreException(SecretStoreFailure.InvalidReference);
            var path = WindowsSecretDirectory.Normalize(root);
            var sid = WindowsSecretDirectory.CurrentSid(serviceSid);
            if (Directory.Exists(path) || File.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                throw new SecretStoreException(SecretStoreFailure.AlreadyExists);
            new DirectoryInfo(path).Create(WindowsSecretDirectory.PrivateAcl(sid));
            WindowsSecretDirectory.Validate(path, sid);
            if (Directory.EnumerateFileSystemEntries(path).Any()) throw Unavailable();
            var context = ScopeContext(scope, serviceSid);
            var encrypted = ProtectedData.Protect(context, ScopeEntropy(), DataProtectionScope.CurrentUser);
            try { WriteNewDurable(Path.Combine(path, ScopeFile), encrypted, sid); }
            finally { CryptographicOperations.ZeroMemory(encrypted); }
        }
        catch (Exception ex) when (IsProviderError(ex)) { throw Unavailable(); }
    }

    public Task CreateAsync(FiscalSecretReference reference, ReadOnlyMemory<byte> candidate, CancellationToken ct = default) =>
        Guard(async () =>
        {
            ValidateReference(reference);
            if (candidate.Length is < 1 or > MaximumCandidateBytes)
                throw new SecretStoreException(SecretStoreFailure.InvalidCandidate);
            ct.ThrowIfCancellationRequested();
            var sid = ValidateRoot();
            using var gate = Lock(reference, sid);
            EnsureNotRevoked(reference, sid);
            if (File.Exists(BlobPath(reference))) throw new SecretStoreException(SecretStoreFailure.AlreadyExists);
            var plain = candidate.ToArray();
            byte[]? encrypted = null;
            string? temporary = null;
            try
            {
                encrypted = ProtectedData.Protect(plain, ReferenceEntropy(reference), DataProtectionScope.CurrentUser);
                temporary = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".pending");
                await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    WindowsSecretDirectory.SetFileOwner(temporary, sid);
                    WindowsSecretDirectory.ValidateFile(temporary, sid);
                    await file.WriteAsync(encrypted, ct);
                    file.Flush(flushToDisk: true);
                }
                ct.ThrowIfCancellationRequested();
                ValidateRoot();
                File.Move(temporary, BlobPath(reference), overwrite: false);
                temporary = null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
                if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
                if (temporary is not null) File.Delete(temporary); // Sólo bytes cifrados.
            }
        });

    public async Task<SecretBuffer> OpenAsync(FiscalSecretReference reference, CancellationToken ct = default)
    {
        SecretBuffer? result = null;
        await Guard(async () =>
        {
            ValidateReference(reference);
            ct.ThrowIfCancellationRequested();
            var sid = ValidateRoot();
            using var gate = Lock(reference, sid);
            EnsureNotRevoked(reference, sid);
            var cipher = await ReadBoundedAsync(BlobPath(reference), sid, ct);
            byte[]? plain = null;
            try
            {
                plain = ProtectedData.Unprotect(cipher, ReferenceEntropy(reference), DataProtectionScope.CurrentUser);
                if (plain.Length is < 1 or > MaximumCandidateBytes) throw Unavailable();
                result = SecretBuffer.CopyFrom(plain);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(cipher);
                if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            }
        });
        return result!;
    }

    public Task RevokeAsync(FiscalSecretReference reference, CancellationToken ct = default) => Guard(() =>
    {
        ValidateReference(reference);
        ct.ThrowIfCancellationRequested();
        var sid = ValidateRoot();
        using var gate = Lock(reference, sid);
        // Tombstone incluso si no existe el candidato: esa referencia no podrá instalarse después.
        var tombstone = RevocationPath(reference);
        if (!File.Exists(tombstone)) WriteNewDurable(tombstone, [1], sid);
        WindowsSecretDirectory.ValidateFile(tombstone, sid);
        return Task.CompletedTask;
    });

    private SecurityIdentifier ValidateRoot()
    {
        var sid = WindowsSecretDirectory.CurrentSid(_serviceSid);
        WindowsSecretDirectory.Validate(_root, sid);
        var marker = Path.Combine(_root, ScopeFile);
        WindowsSecretDirectory.ValidateFile(marker, sid);
        using var file = new FileStream(marker, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is < 1 or > 8192) throw Unavailable();
        var cipher = new byte[(int)file.Length];
        file.ReadExactly(cipher);
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(cipher, ScopeEntropy(), DataProtectionScope.CurrentUser);
            if (!CryptographicOperations.FixedTimeEquals(plain, ScopeContext(_scope, _serviceSid))) throw Unavailable();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipher);
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
        }
        return sid;
    }

    private void ValidateReference(FiscalSecretReference reference)
    {
        if (!reference.IsValid || reference.Scope != _scope)
            throw new SecretStoreException(SecretStoreFailure.InvalidReference);
    }

    private FileStream Lock(FiscalSecretReference reference, SecurityIdentifier sid)
    {
        var path = Path.Combine(_root, Name(reference) + ".lock");
        // El archivo permanece: borrarlo al liberar permite que dos procesos bloqueen inodos distintos.
        if (File.Exists(path)) WindowsSecretDirectory.ValidateFile(path, sid);
        var existed = File.Exists(path);
        var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (!existed) WindowsSecretDirectory.SetFileOwner(path, sid);
            WindowsSecretDirectory.ValidateFile(path, sid);
            return file;
        }
        catch { file.Dispose(); throw; }
    }

    private void EnsureNotRevoked(FiscalSecretReference reference, SecurityIdentifier sid)
    {
        var path = RevocationPath(reference);
        if (!File.Exists(path)) return;
        WindowsSecretDirectory.ValidateFile(path, sid);
        throw new SecretStoreException(SecretStoreFailure.Revoked);
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, SecurityIdentifier sid, CancellationToken ct)
    {
        WindowsSecretDirectory.ValidateFile(path, sid);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (file.Length is < 1 or > MaximumCipherBytes) throw Unavailable();
        var bytes = new byte[(int)file.Length];
        try { await file.ReadExactlyAsync(bytes, ct); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }

    private static void WriteNewDurable(string path, byte[] data, SecurityIdentifier sid)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        WindowsSecretDirectory.SetFileOwner(path, sid);
        WindowsSecretDirectory.ValidateFile(path, sid);
        file.Write(data);
        file.Flush(flushToDisk: true);
    }

    private string BlobPath(FiscalSecretReference reference) => Path.Combine(_root, Name(reference) + ".candidate");
    private string RevocationPath(FiscalSecretReference reference) => Path.Combine(_root, Name(reference) + ".revoked");
    private string Name(FiscalSecretReference reference) => Convert.ToHexString(SHA256.HashData(ReferenceEntropy(reference)));
    private byte[] ReferenceEntropy(FiscalSecretReference reference) => Encoding.UTF8.GetBytes(
        $"GoDental.Candidate.v1|{reference.Scope.IssuerId:N}|{(int)reference.Scope.Environment}|{(int)reference.Purpose}|{reference.Version:N}|{_serviceSid}");
    private static byte[] ScopeContext(FiscalSecretScope scope, string sid) =>
        Encoding.UTF8.GetBytes($"GoDental.Scope.v1|{scope.IssuerId:N}|{(int)scope.Environment}|{sid}");
    private static byte[] ScopeEntropy() => "GoDental.Scope.v1"u8.ToArray();
    private static SecretStoreException Unavailable() => new(SecretStoreFailure.Unavailable);
    private static bool IsProviderError(Exception ex) => ex is IOException or UnauthorizedAccessException or
        CryptographicException or System.Security.SecurityException or ArgumentException or System.ComponentModel.Win32Exception;
    private static async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (IsProviderError(ex)) { throw Unavailable(); }
    }
}
