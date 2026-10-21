namespace GoDental.Application.DTOs;

public enum ReceiptKind { Sale, Delivery }
public sealed record PrintLayout(decimal WidthMm = 215.9m, decimal HeightMm = 279.4m, decimal MarginMm = 11.3m)
{
    public static PrintLayout Letter => new();
    public void Validate(bool narrow = false)
    {
        if (WidthMm < (narrow ? 58 : 140) || WidthMm > 300 || HeightMm < 80 || HeightMm > 1000
            || MarginMm < 2 || MarginMm > 25 || WidthMm - 2 * MarginMm < (narrow ? 46 : 110) || HeightMm - 2 * MarginMm < 60)
            throw new ArgumentException("Revisa las medidas: ancho 58–300 mm (hoja: mínimo 140), alto 80–1000 mm y margen 2–25 mm; conserva espacio para el contenido.");
    }
}
public sealed record LabelPrintOptions(decimal WidthMm = 50, decimal HeightMm = 30, int Copies = 1,
    string Paper = "Etiqueta", decimal MarginMm = 5, decimal GapMm = 2)
{
    public void Validate()
    {
        if (WidthMm < 20 || WidthMm > 200 || HeightMm < 28 || HeightMm > 150 || Copies < 1 || Copies > 200
            || Paper is not ("Etiqueta" or "Carta" or "A4") || MarginMm < 2 || MarginMm > 25 || GapMm < 0 || GapMm > 15)
            throw new ArgumentException("Etiqueta: ancho 20–200 mm, alto 28–150 mm, 1–200 copias, margen 2–25 mm y separación 0–15 mm.");
    }
}
public sealed record LabelContent(string ProductName, string Sku, string Code, string Type);
public sealed record PrintPreferences(PrintLayout? Receipt = null, ReceiptKind Kind = ReceiptKind.Sale, LabelPrintOptions? Labels = null);
