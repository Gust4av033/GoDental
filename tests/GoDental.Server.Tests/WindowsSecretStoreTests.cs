using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using GoDental.Application.DTOs;
using GoDental.Infrastructure.Security;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace GoDental.Server.Tests;

public class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "DPAPI/ACL requieren Windows real; no se sustituyen por un mock.";
    }
}

public sealed class WindowsAdminFactAttribute : WindowsFactAttribute
{
    public WindowsAdminFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var current = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator) &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
            Skip = "La prueba con una cuenta Windows sintética requiere un runner aislado elevado; CI exige ejecutarla.";
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsSecretStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GoDental-secret-synthetic-" + Guid.NewGuid().ToString("N"));
    private readonly FiscalSecretScope _scope = new(Guid.NewGuid(), FiscalSecretEnvironment.Test);
    private readonly string _sid = WindowsIdentity.GetCurrent().User!.Value;
    private readonly byte[] _synthetic = RandomNumberGenerator.GetBytes(96);
    private WindowsFiscalSecretCandidateStore Store()
    {
        WindowsFiscalSecretCandidateStore.InitializeNew(_root, _scope, _sid);
        return new(_root, _scope, _sid);
    }
    private FiscalSecretReference Reference(FiscalSecretPurpose purpose = FiscalSecretPurpose.ApiCredential) => new(_scope, purpose, Guid.NewGuid());

    [WindowsFact]
    public async Task LocalCommandsProvisionAndCheckOnlySyntheticMaterialWithoutSqlOrHttps()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GoDental.Secrets.Synthetic." + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(0, await Command("--initialize-secret-store"));
            Assert.Single(Directory.GetFiles(root));
            Assert.Equal(1, await Command("--initialize-secret-store"));
            Assert.Equal(0, await Command("--check-secret-store"));
            Assert.Single(Directory.GetFiles(root));
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }

        async Task<int> Command(string command)
        {
            using var process = new Process { StartInfo = new("dotnet")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            } };
            process.StartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
            process.StartInfo.ArgumentList.Add(command);
            process.StartInfo.Environment["SecretStore__Root"] = root;
            process.StartInfo.Environment["SecretStore__ServiceSid"] = _sid;
            process.StartInfo.Environment["SecretStore__IssuerId"] = _scope.IssuerId.ToString();
            process.StartInfo.Environment["SecretStore__Environment"] = "TEST";
            process.StartInfo.Environment["Server__SqlServer"] = "";
            process.StartInfo.Environment["Server__Database"] = "";
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var output = (await stdout) + (await stderr);
            Assert.DoesNotContain(root, output);
            Assert.DoesNotContain("BEGIN PRIVATE", output);
            return process.ExitCode;
        }
    }

    [WindowsFact]
    public async Task AnotherProcessHoldingReferenceLockBlocksReadingAndRevocation()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var lockFile = Directory.GetFiles(_root, "*.lock").Single();
        using var process = new Process { StartInfo = new("powershell.exe")
        {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        } };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
            $"$f=[IO.File]::Open('{lockFile}',[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); try {{ [Console]::WriteLine('LOCKED'); [Console]::ReadLine() | Out-Null }} finally {{ $f.Dispose() }}" })
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        try
        {
            Assert.Equal("LOCKED", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20)));
            await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(reference));
            await Assert.ThrowsAsync<SecretStoreException>(() => store.RevokeAsync(reference));
        }
        finally
        {
            await process.StandardInput.WriteLineAsync("release");
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        }
        Assert.Equal(0, process.ExitCode);
        using var opened = await store.OpenAsync(reference);
        Assert.Equal(_synthetic, opened.Memory.ToArray());
    }

    [WindowsFact]
    public async Task CiphertextSurvivesNewInstanceAndNeverContainsPlaintext()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var ciphertext = await File.ReadAllBytesAsync(Directory.GetFiles(_root, "*.candidate").Single());
        Assert.False(ciphertext.AsSpan().IndexOf(_synthetic) >= 0);
        using var opened = await new WindowsFiscalSecretCandidateStore(_root, _scope, _sid).OpenAsync(reference);
        Assert.Equal(_synthetic, opened.Memory.ToArray());
        Assert.Empty(Directory.GetFiles(_root, "*.pending"));
    }

    [WindowsFact]
    public async Task VersionRotationNeverOverwritesOrFallsBackToAnotherCandidate()
    {
        var store = Store();
        var first = Reference();
        var next = Reference();
        await store.CreateAsync(first, _synthetic);
        byte[] different = [5, 6, 7];
        await store.CreateAsync(next, different);
        var duplicate = await Assert.ThrowsAsync<SecretStoreException>(() => store.CreateAsync(first, different));
        Assert.Equal(SecretStoreFailure.AlreadyExists, duplicate.Failure);
        await store.RevokeAsync(first);
        var revoked = await Assert.ThrowsAsync<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root, _scope, _sid).OpenAsync(first));
        Assert.Equal(SecretStoreFailure.Revoked, revoked.Failure);
        using var opened = await store.OpenAsync(next);
        Assert.Equal(different, opened.Memory.ToArray());
        await store.RevokeAsync(first); // Idempotente, sin revivir material.
        await Assert.ThrowsAsync<SecretStoreException>(() => store.CreateAsync(first, _synthetic));
    }

    [WindowsFact]
    public async Task RevocationBeforeInstallationReservesReferenceAsRevoked()
    {
        var store = Store();
        var reference = Reference();
        await store.RevokeAsync(reference);
        var error = await Assert.ThrowsAsync<SecretStoreException>(() => store.CreateAsync(reference, _synthetic));
        Assert.Equal(SecretStoreFailure.Revoked, error.Failure);
    }

    [WindowsFact]
    public async Task WrongIssuerEnvironmentPurposeAndVersionCannotReadMaterial()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        foreach (var wrong in new[]
        {
            reference with { Scope = new(Guid.NewGuid(), FiscalSecretEnvironment.Test) },
            reference with { Scope = _scope with { Environment = FiscalSecretEnvironment.Production } },
            reference with { Purpose = FiscalSecretPurpose.SigningMaterial },
            reference with { Version = Guid.NewGuid() },
            reference with { Version = Guid.Empty }
        }) await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(wrong));
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root, _scope with { Environment = FiscalSecretEnvironment.Production }, _sid));
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root, new(Guid.NewGuid(), FiscalSecretEnvironment.Test), _sid));
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root, _scope, "S-1-5-18" == _sid ? "S-1-5-19" : "S-1-5-18"));
    }

    [WindowsFact]
    public async Task SwappingCiphertextAcrossPurposeOrVersionFailsAuthentication()
    {
        var store = Store();
        var one = Reference();
        var two = Reference(FiscalSecretPurpose.SigningMaterial);
        await store.CreateAsync(one, _synthetic);
        var firstFile = Directory.GetFiles(_root, "*.candidate").Single();
        await store.CreateAsync(two, _synthetic);
        var secondFile = Directory.GetFiles(_root, "*.candidate").Single(p => p != firstFile);
        File.Copy(firstFile, secondFile, overwrite: true);
        await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(two));
        using var original = await store.OpenAsync(one);
        Assert.Equal(_synthetic, original.Memory.ToArray());
    }

    [WindowsFact]
    public async Task CorruptAndOversizedBlobsFailWithoutExposingProviderDetails()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var path = Directory.GetFiles(_root, "*.candidate").Single();
        await File.WriteAllBytesAsync(path, [1, 3, 5]);
        var error = await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(reference));
        Assert.DoesNotContain(_root, error.ToString());
        Assert.Null(error.InnerException);
        await using (var file = File.OpenWrite(path)) file.SetLength(1024 * 1024);
        await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(reference));
    }

    [WindowsFact]
    public async Task EmptyOversizedAndCancelledWritesDoNotPublishMaterial()
    {
        var store = Store();
        await Assert.ThrowsAsync<SecretStoreException>(() => store.CreateAsync(Reference(), ReadOnlyMemory<byte>.Empty));
        await Assert.ThrowsAsync<SecretStoreException>(() => store.CreateAsync(Reference(), new byte[65537]));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(Reference(), _synthetic, cancelled.Token));
        Assert.Empty(Directory.GetFiles(_root, "*.candidate"));
        Assert.Empty(Directory.GetFiles(_root, "*.pending"));
    }

    [WindowsFact]
    public async Task TwoStoreInstancesCannotReplaceOneReference()
    {
        var store = Store();
        var other = new WindowsFiscalSecretCandidateStore(_root, _scope, _sid);
        var reference = Reference();
        var outcomes = await Task.WhenAll(Attempt(store), Attempt(other));
        Assert.Single(outcomes, succeeded => succeeded);
        using var opened = await store.OpenAsync(reference);
        Assert.Equal(_synthetic, opened.Memory.ToArray());
        async Task<bool> Attempt(WindowsFiscalSecretCandidateStore candidateStore)
        {
            try { await candidateStore.CreateAsync(reference, _synthetic); return true; }
            catch (SecretStoreException) { return false; }
        }
    }

    [WindowsFact]
    public async Task BroadDirectoryOrFileAclIsRejectedOnEveryOperation()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var directory = new DirectoryInfo(_root);
        var original = directory.GetAccessControl();
        var wide = directory.GetAccessControl();
        wide.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        directory.SetAccessControl(wide);
        await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(reference));
        await Assert.ThrowsAsync<SecretStoreException>(() => store.RevokeAsync(reference));
        directory.SetAccessControl(original);
        var file = new FileInfo(Directory.GetFiles(_root, "*.candidate").Single());
        var fileAcl = file.GetAccessControl();
        fileAcl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(fileAcl);
        await Assert.ThrowsAsync<SecretStoreException>(() => store.OpenAsync(reference));
    }

    [WindowsFact]
    public void InitializationNeverRepairsOrRebindsAnExistingRoot()
    {
        Store();
        var before = File.ReadAllBytes(Path.Combine(_root, "scope.bin"));
        Assert.Throws<SecretStoreException>(() => WindowsFiscalSecretCandidateStore.InitializeNew(_root,
            _scope with { Environment = FiscalSecretEnvironment.Production }, _sid));
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(_root, "scope.bin")));
    }

    [WindowsFact]
    public void NetworkAndAlternateStreamPathsAreRejected()
    {
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(@"\\localhost\C$\vault", _scope, _sid));
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root + ":stream", _scope, _sid));
        Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore("relative", _scope, _sid));
    }

    [WindowsFact]
    public async Task ReparsePointRootIsRejected()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var link = _root + "-link";
        try
        {
            using var junction = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = $"/c mklink /J \"{link}\" \"{_root}\"",
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            })!;
            await junction.WaitForExitAsync();
            Assert.Equal(0, junction.ExitCode);
            Assert.Throws<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(link, _scope, _sid));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }

    [WindowsFact]
    public async Task RestoringEncryptedFilesUnderSameServiceIdentityPreservesRevocation()
    {
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        await store.RevokeAsync(reference);
        var backup = Directory.GetFiles(_root).ToDictionary(p => Path.GetFileName(p), File.ReadAllBytes);
        Directory.Delete(_root, recursive: true);
        // Restauración en ruta original y bajo misma identidad/perfil. No prueba recuperación DPAPI en otro host.
        WindowsFiscalSecretCandidateStore.InitializeNew(_root, _scope, _sid);
        foreach (var (name, bytes) in backup)
        {
            var path = Path.Combine(_root, name!);
            File.WriteAllBytes(path, bytes);
            var acl = new FileSecurity();
            acl.SetOwner(new SecurityIdentifier(_sid));
            new FileInfo(path).SetAccessControl(acl);
        }
        var error = await Assert.ThrowsAsync<SecretStoreException>(() => new WindowsFiscalSecretCandidateStore(_root, _scope, _sid).OpenAsync(reference));
        Assert.Equal(SecretStoreFailure.Revoked, error.Failure);
    }

    [WindowsAdminFact]
    public async Task AnotherWindowsAccountCannotReadFilesOrDecryptCopiedBlob()
    {
        using var current = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Esta prueba de identidad requiere ejecutar el runner Windows elevado con cuenta sintética local.");
        var store = Store();
        var reference = Reference();
        await store.CreateAsync(reference, _synthetic);
        var marker = File.ReadAllBytes(Path.Combine(_root, "scope.bin"));
        var user = "GDtest" + Guid.NewGuid().ToString("N")[..12];
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36)) + "aA9!";
        var account = new NativeUser { Name = user, Password = password, Privilege = 1, Flags = 0x0001 | 0x0200 | 0x10000 };
        var created = false;
        try
        {
            // NetAPI evita depender de módulos/política de PowerShell; nunca imprime la contraseña.
            var status = NetUserAdd(null, 1, ref account, out var parameter);
            Assert.True(status == 0, $"NetUserAdd sintético: estado {status}, parámetro {parameter}.");
            created = true;
            var users = ((NTAccount)new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null).Translate(typeof(NTAccount)))
                .Value.Split('\\').Last();
            var member = new NativeGroupMember { Name = Environment.MachineName + "\\" + user };
            Assert.Equal(0u, NetLocalGroupAddMembers(null, users, 3, ref member, 1));
            Assert.True(LogonUser(user, ".", password, 2, 0, out var token));
            using (token)
                WindowsIdentity.RunImpersonated(token, () =>
                {
                    using var other = WindowsIdentity.GetCurrent();
                    Assert.NotEqual(_sid, other.User!.Value);
                    Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllBytes(Path.Combine(_root, "scope.bin")));
                    // Copia en memoria accesible deliberadamente: la protección no depende sólo de la ACL.
                    Assert.Throws<CryptographicException>(() => ProtectedData.Unprotect(marker,
                        "GoDental.Scope.v1"u8.ToArray(), DataProtectionScope.CurrentUser));
                });
        }
        finally { if (created) Assert.Equal(0u, NetUserDel(null, user)); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeUser
    {
        public string Name;
        public string Password;
        public uint PasswordAge;
        public uint Privilege;
        public string? HomeDirectory;
        public string? Comment;
        public uint Flags;
        public string? ScriptPath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeGroupMember { public string Name; }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetUserAdd(string? server, uint level, ref NativeUser user, out uint parameterError);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetUserDel(string? server, string userName);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetLocalGroupAddMembers(string? server, string groupName, uint level,
        ref NativeGroupMember member, uint count);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(string userName, string domain, string password, int logonType,
        int logonProvider, out SafeAccessTokenHandle token);

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_synthetic);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
