using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Wpf.Dialogs;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CheckoutModel : ObservableObject
{
    public const string ManualCardNotice = "Tarjeta: registra únicamente un cobro ya aprobado en la terminal externa y su referencia. GoDental no procesa ni verifica el cobro. No ingreses número de tarjeta ni CVV.";
    public bool HasManualCard => Methods.Any(m => m.Code == "TARJETA");
    public string CardNotice => ManualCardNotice;
    public SaleQuote Quote { get; }
    public IReadOnlyList<PaymentMethodData> Methods { get; }
    public ObservableCollection<SalePaymentData> Payments { get; } = [];
    [ObservableProperty] private PaymentMethodData? method;
    [ObservableProperty] private SalePaymentData? selected;
    [ObservableProperty] private string amount;
    [ObservableProperty] private string reference = "";
    [ObservableProperty] private string cashTendered = "0";
    [ObservableProperty] private string statusMessage = "Agrega los importes aplicados. Efectivo recibido se registra por separado.";
    public CheckoutModel(SaleQuote quote, IReadOnlyList<PaymentMethodData> methods) { Quote = quote; Methods = methods; Method = methods.FirstOrDefault(); amount = quote.Total.ToString("0.00"); }
    public decimal Tendered => Parse(CashTendered, "efectivo recibido");
    public IReadOnlyList<SalePayment> Snapshot() => Payments.Select(p => new SalePayment(p.MethodId, p.Amount, p.Reference)).ToArray();
    [RelayCommand] private void Add()
    {
        try
        {
            if (Method is null) throw new ArgumentException("Selecciona una forma de pago.");
            var value = Parse(Amount, "importe aplicado");
            if (value <= 0 || Payments.Count >= 10) throw new ArgumentException("Usa importes positivos y hasta diez pagos.");
            if ((Method.RequiresReference || Method.Code == "TARJETA") && string.IsNullOrWhiteSpace(Reference)) throw new ArgumentException("Esta forma de pago requiere referencia.");
            if (Reference.Length > 150) throw new ArgumentException("Usa una referencia de hasta 150 caracteres.");
            Payments.Add(new() { MethodId = Method.Id, Method = Method.Name, Amount = value, Reference = string.IsNullOrWhiteSpace(Reference) ? null : Reference.Trim() });
            StatusMessage = "Pago agregado. El servicio verificará el total y determinará el cambio al confirmar.";
        }
        catch (ArgumentException ex) { StatusMessage = ex.Message; }
    }
    [RelayCommand] private void Remove() { if (Selected is not null) Payments.Remove(Selected); }
    internal static decimal Parse(string text, string label)
    {
        if (!decimal.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.CurrentCulture, out var n)) throw new ArgumentException("Revisa " + label + ".");
        return n;
    }
}
public sealed record CartLineModel(int ProductId, string Sku, string Name, decimal Quantity, decimal DiscountPercent, long? LotId = null);
