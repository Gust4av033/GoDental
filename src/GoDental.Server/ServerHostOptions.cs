namespace GoDental.Server;

public sealed class ServerHostOptions
{
    public string SqlServer { get; init; } = "";
    public string Database { get; init; } = "";
    public bool LocalDevelopmentSqlCertificate { get; init; }
}
