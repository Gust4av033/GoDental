namespace GoDental.Server;

public sealed class ServerHostOptions
{
    public string Url { get; init; } = "https://localhost:7443";
    public string SqlServer { get; init; } = "";
    public string Database { get; init; } = "";
    public bool LocalDevelopmentSqlCertificate { get; init; }
    public string ServiceSid { get; init; } = "";
    public string HttpsCertificateThumbprint { get; init; } = "";
}
