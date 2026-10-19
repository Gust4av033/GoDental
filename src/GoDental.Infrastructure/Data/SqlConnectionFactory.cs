using System.Data.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace GoDental.Infrastructure.Data;

public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory, IWorkstationConnectionOpener
{
    private readonly DatabaseOptions _options = options.Value;

    async Task<DbConnection> IWorkstationConnectionOpener.OpenAsync(WorkstationSettings settings, CancellationToken ct) =>
        await OpenAsync(settings, ct);

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException(
                "No se ha configurado Database:ConnectionString en la configuración local.");
        }

        return await OpenConnectionStringAsync(_options.ConnectionString, cancellationToken);
    }

    // Diagnóstico inicial con identidad de Windows; no sustituye la cuenta de aplicación de SEC-01.
    public Task<SqlConnection> OpenAsync(WorkstationSettings settings, CancellationToken cancellationToken)
    {
        if (WorkstationSettingsValidator.Validate(settings) is { } error)
            throw new ArgumentException(error, nameof(settings));
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            InitialCatalog = settings.Database.Trim(),
            IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = settings.LocalDevelopment,
            ConnectTimeout = 5,
            ConnectRetryCount = 0,
            ApplicationName = "GoDental.Configuration"
        };
        return OpenConnectionStringAsync(builder.ConnectionString, cancellationToken);
    }

    private static async Task<SqlConnection> OpenConnectionStringAsync(string connectionString, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
