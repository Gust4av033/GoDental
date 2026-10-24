using System.Globalization;
using System.Text;
using System.Text.Json;
namespace GoDental.Application.Localization;

public sealed record CountryOption(string Code, string Name, string[] Aliases);
public sealed record MunicipalityOption(string Name, string[] Districts);
public sealed record DepartmentOption(string Name, MunicipalityOption[] Municipalities);
public static class DirectoryCatalog
{
    public static IReadOnlyList<CountryOption> Countries { get; } = Array.AsReadOnly(Read<CountryOption[]>("Countries.es.json").OrderBy(x => x.Name, StringComparer.Create(CultureInfo.GetCultureInfo("es-SV"), false)).ToArray());
    public static IReadOnlyList<DepartmentOption> Departments { get; } = Array.AsReadOnly(Read<DepartmentOption[]>("ElSalvador.json"));
    private static T Read<T>(string file)
    {
        using var stream = typeof(DirectoryCatalog).Assembly.GetManifestResourceStream("GoDental.Application.Localization.Data." + file)!;
        return JsonSerializer.Deserialize<T>(stream)!;
    }
    public static CountryOption? Country(string? value) => Countries.FirstOrDefault(c => Same(c.Name, value) || Same(c.Code, value) || c.Aliases.Any(a => Same(a, value)));
    public static bool Same(string? a, string? b) => Key(a) == Key(b);
    private static string Key(string? s) => string.Concat((s ?? "").Trim().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    public static string? LocationError(string? department, string? municipality, string? district)
    {
        if (string.IsNullOrWhiteSpace(department)) return string.IsNullOrWhiteSpace(municipality) && string.IsNullOrWhiteSpace(district) ? null : "Selecciona primero el departamento.";
        var d = Departments.FirstOrDefault(d => d.Name == department);
        if (d is null) return "Selecciona un departamento del catálogo.";
        if (string.IsNullOrWhiteSpace(municipality)) return string.IsNullOrWhiteSpace(district) ? null : "Selecciona primero el municipio.";
        var m = d.Municipalities.FirstOrDefault(m => m.Name == municipality);
        if (m is null) return "El municipio debe pertenecer al departamento seleccionado.";
        return string.IsNullOrWhiteSpace(district) || m.Districts.Contains(district) ? null : "El distrito debe pertenecer al municipio seleccionado.";
    }
}
