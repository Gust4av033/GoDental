using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

internal static class DevelopmentDatabase
{
    // Sólo el ejecutable de pruebas permite LocalDB; no cambia la conexión del producto.
    public static string TestServer { get; set; } = "localhost";
    public static string ConnectionString(string database) => new SqlConnectionStringBuilder
    {
        DataSource = TestServer, InitialCatalog = database, IntegratedSecurity = true,
        Encrypt = SqlConnectionEncryptOption.Mandatory, TrustServerCertificate = true, ConnectTimeout = 5
    }.ConnectionString;

    public static async Task InstallAsync(string database, string scriptFolder, bool includeServerIdentity = true)
    {
        if (!Regex.IsMatch(database, @"^GoDental_(Development|Tests_[A-Za-z0-9]+)$")) throw new ArgumentException("Destino fuera del entorno de desarrollo.");
        await using var master = new SqlConnection(ConnectionString("master")); await master.OpenAsync();
        var exists = await master.ExecuteScalarAsync<int?>("SELECT DB_ID(@database)", new { database });
        if (exists is null) await master.ExecuteAsync($"CREATE DATABASE [{database}];");
        await using var connection = new SqlConnection(ConnectionString(database)); await connection.OpenAsync();
        if (exists is null)
            await connection.ExecuteAsync("EXEC sys.sp_addextendedproperty @name=N'GoDental.Development', @value=N'GoDental-LocalTools';");
        else
        {
            var marker = await connection.ExecuteScalarAsync<string>("SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'GoDental.Development'");
            if (marker is not ("GoDental-LocalTools" or "CreatedByCodex-2026-10-17")) throw new InvalidOperationException("La base existente no tiene un marcador reconocido de desarrollo.");
            if (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE object_id NOT IN(ISNULL(OBJECT_ID(N'seg.SesionesServidor'),0),ISNULL(OBJECT_ID(N'com.SegmentosProspecto'),0),ISNULL(OBJECT_ID(N'com.SeguimientosProspecto'),0),ISNULL(OBJECT_ID(N'ven.AvisosCobranza'),0),ISNULL(OBJECT_ID(N'ven.RepartosMvp'),0),ISNULL(OBJECT_ID(N'ven.RepartoLineasMvp'),0),ISNULL(OBJECT_ID(N'sis.OperacionesMvp'),0),ISNULL(OBJECT_ID(N'ven.PedidosMvp'),0),ISNULL(OBJECT_ID(N'ven.PedidoLineasMvp'),0),ISNULL(OBJECT_ID(N'ven.PedidoEventosMvp'),0))") != 72)
                throw new InvalidOperationException("La instalación existente está incompleta; requiere revisión manual.");
        }
        foreach (var file in Directory.GetFiles(scriptFolder, "0*.sql").Order())
        {
            if (!includeServerIdentity && string.CompareOrdinal(Path.GetFileName(file)[..3], "026") > 0) continue;
            // La baseline se conserva: sólo se cambia el destino en la copia en memoria.
            // Sobre una base completa existente se ejecutan únicamente migraciones posteriores a 008.
            if (exists is not null && string.CompareOrdinal(Path.GetFileName(file)[..3], "008") <= 0) continue;
            await ExecuteScriptAsync(connection, file, database);
            Console.WriteLine("SQL " + Path.GetFileName(file));
        }
        var counts = await connection.QuerySingleAsync("SELECT (SELECT COUNT(*) FROM sys.tables) Tables,(SELECT COUNT(*) FROM sys.views) Views,(SELECT COUNT(*) FROM sys.procedures) Procedures");
        if ((int)counts.Tables != (includeServerIdentity ? 82 : 81) || (int)counts.Views != 8 ||
            (int)counts.Procedures != (includeServerIdentity ? 22 : 18)) throw new InvalidOperationException("Objetos de baseline/migraciones incompletos.");
    }

    public static async Task ReapplySeedAsync(string database, string scriptFolder)
    {
        if (!Regex.IsMatch(database, @"^GoDental_Tests_[A-Za-z0-9]+$")) throw new ArgumentException("La prueba sólo puede resembrar una base aislada.");
        await using var connection = new SqlConnection(ConnectionString(database)); await connection.OpenAsync();
        var marker = await connection.ExecuteScalarAsync<string>("SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'GoDental.Development'");
        if (marker != "GoDental-LocalTools") throw new InvalidOperationException("La base de pruebas no tiene el marcador esperado.");
        await ExecuteScriptAsync(connection, Path.Combine(scriptFolder, "007_Seed.sql"), database);
    }

    private static async Task ExecuteScriptAsync(SqlConnection connection, string file, string database)
    {
        var script = File.ReadAllText(file).Replace("N'GoDental'", $"N'{database}'").Replace("[GoDental]", $"[{database}]");
        script = Regex.Replace(script, @"\bUSE\s+GoDental\s*;", $"USE [{database}];", RegexOptions.IgnoreCase);
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*(?:--[^\r\n]*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await connection.ExecuteAsync(batch, commandTimeout: 120);
    }
}

