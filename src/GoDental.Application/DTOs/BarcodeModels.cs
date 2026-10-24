namespace GoDental.Application.DTOs;

public sealed class BarcodeData
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Active { get; set; }
    public bool Generated { get; set; }
    public bool Primary { get; set; }
}
public sealed record ProductBarcodes(byte[] ProductVersion, IReadOnlyList<BarcodeData> Items);
