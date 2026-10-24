using System.Security.Cryptography;
using GoDental.Application.DTOs;
using GoDental.Infrastructure.Security;

namespace GoDental.Server.Security;

internal static class SecretStoreCommand
{
    internal static async Task<int> RunAsync(bool initialize)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("El almacén privado requiere Windows con DPAPI y ACL NTFS.");
            return 1;
        }
        // Sólo configuración no secreta de entorno. Ningún argumento de importación/contraseña.
        var config = new ConfigurationBuilder().AddEnvironmentVariables("SecretStore__").Build();
        var root = config["Root"];
        var sid = config["ServiceSid"];
        var environment = config["Environment"] switch
        {
            "TEST" => FiscalSecretEnvironment.Test,
            "PROD" => FiscalSecretEnvironment.Production,
            _ => (FiscalSecretEnvironment)0
        };
        if (!Guid.TryParse(config["IssuerId"], out var issuer) || issuer == Guid.Empty || environment == 0 ||
            string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(sid))
        {
            Console.Error.WriteLine("Falta configuración no secreta válida: Root, ServiceSid, IssuerId y Environment TEST/PROD.");
            return 1;
        }
        string? probeRoot = null;
        byte[]? synthetic = null;
        try
        {
            // El comando de despliegue sólo admite una raíz directa privada bajo ProgramData.
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            if (!string.Equals(Path.GetDirectoryName(full), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("GoDental.Secrets.", StringComparison.Ordinal))
                throw new SecretStoreException(SecretStoreFailure.Unavailable);
            var scope = new FiscalSecretScope(issuer, environment);
            if (initialize)
            {
                WindowsFiscalSecretCandidateStore.InitializeNew(full, scope, sid);
                Console.WriteLine("Raíz privada vacía provisionada. Ningún material fiscal instalado o habilitado.");
                return 0;
            }
            _ = new WindowsFiscalSecretCandidateStore(full, scope, sid);
            probeRoot = Path.Combine(full, "probe-" + Guid.NewGuid().ToString("N"));
            WindowsFiscalSecretCandidateStore.InitializeNew(probeRoot, scope, sid);
            var store = new WindowsFiscalSecretCandidateStore(probeRoot, scope, sid);
            var reference = new FiscalSecretReference(scope, FiscalSecretPurpose.ApiCredential, Guid.NewGuid());
            synthetic = RandomNumberGenerator.GetBytes(64);
            await store.CreateAsync(reference, synthetic);
            using (var material = await new WindowsFiscalSecretCandidateStore(probeRoot, scope, sid).OpenAsync(reference))
                if (!CryptographicOperations.FixedTimeEquals(material.Memory.Span, synthetic))
                    throw new SecretStoreException(SecretStoreFailure.Unavailable);
            await store.RevokeAsync(reference);
            try
            {
                using var unexpected = await store.OpenAsync(reference);
                throw new SecretStoreException(SecretStoreFailure.Unavailable);
            }
            catch (SecretStoreException ex) when (ex.Failure == SecretStoreFailure.Revoked) { }
            Console.WriteLine("Diagnóstico sintético correcto: ACL, DPAPI, persistencia y revocación. Emisión y PROD bloqueadas.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("No se pudo comprobar/provisionar el almacén privado. Revisar identidad, perfil Windows, ruta y ACL.");
            return 1;
        }
        finally
        {
            if (synthetic is not null) CryptographicOperations.ZeroMemory(synthetic);
            if (probeRoot is not null)
            {
                try { Directory.Delete(probeRoot, recursive: true); }
                catch (Exception) { Console.Error.WriteLine("No se pudo retirar el diagnóstico sintético; revisar limpieza local."); }
            }
        }
    }
}
