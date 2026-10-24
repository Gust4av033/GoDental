using System.Data.Common;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using Microsoft.Data.SqlClient;

namespace GoDental.Infrastructure.Data;

public sealed class ServerSqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public ServerSqlConnectionFactory(WorkstationSettings settings, bool developmentEnvironment)
    {
        if (WorkstationSettingsValidator.Validate(settings) is not null ||
            (settings.LocalDevelopment && !developmentEnvironment))
            throw new ArgumentException("Configuración SQL del servidor inválida.");
        _connectionString = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(), InitialCatalog = settings.Database.Trim(),
            IntegratedSecurity = true, Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = settings.LocalDevelopment, ConnectTimeout = 5,
            ConnectRetryCount = 0, ApplicationName = "GoDental.Server"
        }.ConnectionString;
    }

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await VerifyRuntimeIdentityAsync(connection, cancellationToken);
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public static async Task VerifyRuntimeIdentityAsync(DbConnection connection, CancellationToken ct = default)
    {
        var allowed = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT CASE WHEN ISNULL(IS_MEMBER(N'GoDentalServerRuntime'),0)=1
                AND (SELECT COUNT(*) FROM sys.procedures p JOIN sys.schemas s ON s.schema_id=p.schema_id
                    WHERE s.name=N'seg' AND p.name IN(N'ServidorLoginLeer',N'ServidorLoginFinalizar',N'ServidorSesionValidar',N'ServidorSesionRevocar')
                    AND HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(p.name),N'OBJECT',N'EXECUTE')=1)=4
                AND ISNULL(IS_SRVROLEMEMBER(N'sysadmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'securityadmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'serveradmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'setupadmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'processadmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'diskadmin'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'dbcreator'),0)=0
                AND ISNULL(IS_SRVROLEMEMBER(N'bulkadmin'),0)=0
                AND ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,N'CONTROL SERVER'),0)=0
                AND NOT EXISTS(SELECT 1 FROM sys.database_principals WHERE type=N'R'
                    AND name IN(N'db_owner',N'db_securityadmin',N'db_ddladmin',N'db_accessadmin',
                        N'db_datareader',N'db_datawriter',N'db_backupoperator') AND IS_MEMBER(name)=1)
                AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0)=0
                AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE PROCEDURE'),0)=0
                AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CREATE TABLE'),0)=0
                AND ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'IMPERSONATE ANY USER'),0)=0
                AND NOT EXISTS(SELECT 1 FROM sys.schemas WHERE name NOT IN(N'sys',N'INFORMATION_SCHEMA')
                    AND (HAS_PERMS_BY_NAME(name,N'SCHEMA',N'ALTER')=1 OR HAS_PERMS_BY_NAME(name,N'SCHEMA',N'CONTROL')=1))
                AND NOT EXISTS(SELECT 1 FROM sys.objects t JOIN sys.schemas s ON s.schema_id=t.schema_id
                    WHERE t.is_ms_shipped=0 AND t.type IN(N'U',N'V',N'IF',N'TF',N'FT') AND (
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'SELECT')=1 OR
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'INSERT')=1 OR
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'UPDATE')=1 OR
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'DELETE')=1))
                AND NOT EXISTS(SELECT 1 FROM sys.columns c JOIN sys.objects t ON t.object_id=c.object_id
                    JOIN sys.schemas s ON s.schema_id=t.schema_id
                    WHERE t.is_ms_shipped=0 AND t.type IN(N'U',N'V',N'IF',N'TF',N'FT') AND (
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'SELECT',c.name,N'COLUMN')=1 OR
                        HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(t.name),N'OBJECT',N'UPDATE',c.name,N'COLUMN')=1))
                AND NOT EXISTS(SELECT 1 FROM sys.objects p JOIN sys.schemas s ON s.schema_id=p.schema_id
                    WHERE p.is_ms_shipped=0 AND p.type IN(N'P',N'PC',N'FN',N'FS')
                    AND HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(p.name),N'OBJECT',N'EXECUTE')=1
                    AND NOT(s.name=N'seg' AND p.name IN(N'ServidorLoginLeer',N'ServidorLoginFinalizar',N'ServidorSesionValidar',N'ServidorSesionRevocar')))
                THEN 1 ELSE 0 END;
            """, commandTimeout: 10, cancellationToken: ct));
        if (allowed != 1) throw new UnauthorizedAccessException("La identidad SQL del servidor no cumple el rol limitado requerido.");
    }
}
