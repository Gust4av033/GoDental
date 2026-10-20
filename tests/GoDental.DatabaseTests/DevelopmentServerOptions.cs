using GoDental.Application.Validators;

// Selección exclusiva de las herramientas locales de desarrollo/pruebas.
internal static class DevelopmentServerOptions
{
    public static string Parse(string[] args)
    {
        var positions = Enumerable.Range(0, args.Length).Where(i => args[i] == "--server").ToArray();
        var localDbCount = args.Count(a => a == "--localdb");
        if (positions.Length > 1 || localDbCount > 1 || positions.Length != 0 && localDbCount != 0)
            throw new ArgumentException("Selecciona una sola instancia: --server o --localdb.");
        if (localDbCount == 1) return @"(localdb)\MSSQLLocalDB";
        if (positions.Length == 0) return "localhost";

        var index = positions[0] + 1;
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException(@"Falta el servidor después de --server; por ejemplo .\SQLEXPRESS.");

        var server = args[index].Trim();
        if (!WorkstationSettingsValidator.IsLocalServer(server))
            throw new ArgumentException(@"La herramienta admite sólo instancias locales: localhost, . o .\SQLEXPRESS. Usa --localdb para LocalDB.");
        return server;
    }
}
