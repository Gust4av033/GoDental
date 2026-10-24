using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using GoDental.Application.Validators;

namespace GoDental.Server.Security;

public static class ServerDeploymentPolicy
{
    public static bool Valid(ServerHostOptions options, bool development)
    {
        if (WorkstationSettingsValidator.Validate(new(options.SqlServer, options.Database,
                LocalDevelopment: options.LocalDevelopmentSqlCertificate)) is not null ||
            (options.LocalDevelopmentSqlCertificate && !development) || !TryUrl(options.Url, out var url)) return false;
        if (development) return url!.IsLoopback;
        return !string.IsNullOrWhiteSpace(options.ServiceSid) && ValidThumbprint(options.HttpsCertificateThumbprint);
    }

    public static bool TryUrl(string? value, out Uri? url) => Uri.TryCreate(value, UriKind.Absolute, out url) &&
        url.Scheme == "https" && url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0 &&
        url.AbsolutePath == "/" && url.Port > 0 && url.HostNameType is UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6;

    public static bool ValidThumbprint(string? value) => value is { Length: 40 } && value.All(char.IsAsciiHexDigit);

    // TLS estándar del host. No interpreta ni valida formatos de certificado fiscal MH.
    public static bool ValidCertificate(X509Certificate2 certificate, string host, DateTimeOffset now)
    {
        if (!certificate.HasPrivateKey || certificate.NotBefore.ToUniversalTime() > now.UtcDateTime ||
            certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime ||
            !certificate.MatchesHostname(host, allowWildcards: false, allowCommonName: false)) return false;
        var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().SingleOrDefault();
        if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>().Any(x => x.Value == "1.3.6.1.5.5.7.3.1")) return false;
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa is not null) return rsa.KeySize >= 2048;
        using var ec = certificate.GetECDsaPrivateKey();
        return ec is not null && ec.KeySize >= 256;
    }
}
