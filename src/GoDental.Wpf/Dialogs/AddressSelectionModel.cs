using CommunityToolkit.Mvvm.ComponentModel;
using GoDental.Application.DTOs;
using GoDental.Application.Localization;
namespace GoDental.Wpf.Dialogs;

public sealed class AddressSelectionModel : ObservableObject
{
    private readonly CustomerAddressData original;
    private string department, municipality, district;
    public const string Unspecified = "Sin especificar";
    public IReadOnlyList<string> Departments { get; }
    public IReadOnlyList<string> Municipalities => Options(DirectoryCatalog.Departments.FirstOrDefault(x => x.Name == Department)?.Municipalities.Select(x => x.Name), department == original.Department ? original.Municipality : null);
    public IReadOnlyList<string> Districts => Options(DirectoryCatalog.Departments.FirstOrDefault(x => x.Name == Department)?.Municipalities.FirstOrDefault(x => x.Name == Municipality)?.Districts, department == original.Department && municipality == original.Municipality ? original.District : null);
    public string Department { get => department; set { if (value is null || !SetProperty(ref department, value)) return; municipality = district = Unspecified; OnPropertyChanged(nameof(Municipalities)); OnPropertyChanged(nameof(Municipality)); OnPropertyChanged(nameof(Districts)); OnPropertyChanged(nameof(District)); } }
    public string Municipality { get => municipality; set { if (value is null || !SetProperty(ref municipality, value)) return; district = Unspecified; OnPropertyChanged(nameof(Districts)); OnPropertyChanged(nameof(District)); } }
    public string District { get => district; set { if (value is not null) SetProperty(ref district, value); } }
    public bool HasLegacyLocation => DirectoryCatalog.LocationError(original.Department, original.Municipality, original.District) is not null;
    public AddressSelectionModel(CustomerAddressData address)
    {
        original = address with { }; department = address.Department ?? Unspecified; municipality = address.Municipality ?? Unspecified; district = address.District ?? Unspecified;
        Departments = Options(DirectoryCatalog.Departments.Select(x => x.Name), address.Department);
    }
    private static IReadOnlyList<string> Options(IEnumerable<string>? values, string? legacy)
    {
        var list = new List<string> { Unspecified }; list.AddRange(values ?? []);
        if (legacy is not null && !list.Contains(legacy)) list.Add(legacy);
        return list;
    }
    public CustomerAddressData Apply(CustomerAddressData address) => address with { Department = Value(Department, original.Department), Municipality = Value(Municipality, original.Municipality), District = Value(District, original.District) };
    private static string? Value(string selected, string? old) => selected == (old ?? Unspecified) ? old : selected == Unspecified ? null : selected;
}
