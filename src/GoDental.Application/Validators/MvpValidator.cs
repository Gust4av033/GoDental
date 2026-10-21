using GoDental.Application.DTOs;

namespace GoDental.Application.Validators;
public static class MvpValidator
{
    public static bool Money(decimal n, bool positive = false) => n >= (positive ? .01m : 0) && n <= 9999999999999999.99m && decimal.Round(n, 2) == n;
    public static bool Quantity(decimal n) => n > 0 && n <= 99999999999999.9999m && decimal.Round(n, 4) == n;
    public static bool Version(byte[]? v) => v?.Length == 8;
    public static bool Page(MvpFilter f) => f.PageSize is >= 1 and <= 100 && f.Page >= 1 && f.Page <= int.MaxValue / f.PageSize;
    public static bool Reason(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 500;
    public static string? Cart(SaleCart? c)
    {
        if (c is null || c.CashBoxId <= 0 || c.CustomerId is <= 0 || c.PriceListId is <= 0 || c.Items is null || c.Items.Count is < 1 or > 100 || !Money(c.DeliveryCharge) || c.GlobalDiscountPercent is < 0 or > 100 || decimal.Round(c.GlobalDiscountPercent, 4) != c.GlobalDiscountPercent)
            return "Revisa caja, cliente, productos, descuentos y entrega.";
        if (c.Items.Any(i => i is null || i.ProductId <= 0 || !Quantity(i.Quantity) || i.LotId is <= 0 || i.DiscountPercent is < 0 or > 100 || decimal.Round(i.DiscountPercent, 4) != i.DiscountPercent) || c.Items.Select(i => i.ProductId).Distinct().Count() != c.Items.Count)
            return "Usa una línea por producto, cantidades positivas de cuatro decimales y descuentos de hasta cuatro decimales.";
        return null;
    }

    public static string? Validate(object r) => r switch
    {
        ChangeCommercialParameters p => !Operation(p.OperationId, p.Reason) || !Version(p.JustificationVersion) || !Version(p.AuthorizationVersion) || p.Justification < 0 || p.Authorization > 100 || p.Justification > p.Authorization || decimal.Round(p.Justification, 4) != p.Justification || decimal.Round(p.Authorization, 4) != p.Authorization ? "Usa umbrales entre 0 y 100, hasta cuatro decimales y justificación no mayor que autorización. Actualiza antes de editar." : null,
        ConfirmSale s => Cart(s.Cart) ?? (!Operation(s.OperationId, s.Reason) || !Token(s.QuoteToken) || s.ShiftId is <= 0 || s.QuotationId is <= 0 || s.DiscountAuthorizationId is <= 0 || s.Order is { } o && (o.OrderId <= 0 || !Version(o.ExpectedVersion) || s.QuotationId is not null) || !Money(s.CashTendered) || s.Payments is null || s.Payments.Count is < 1 or > 10 || s.Payments.Any(p => p is null || p.MethodId <= 0 || !Money(p.Amount, true) || p.Reference?.Length > 150) ? "Revisa pagos, efectivo, origen, motivo y cálculo aceptado." : null),
        SaveQuotation q => Cart(q.Cart) ?? (!Operation(q.OperationId, q.Reason) || !Token(q.QuoteToken) || q.DiscountAuthorizationId is <= 0 || q.ValidUntil?.TimeOfDay > TimeSpan.Zero ? "Revisa vigencia y cálculo de la cotización." : null),
        DiscountApproval a => Cart(a.Cart) ?? (!Operation(a.OperationId, a.Reason) || !Token(a.QuoteToken) ? "Revisa autorización y motivo." : null),
        InitialInventory i => !Operation(i.OperationId, i.Reason) || i.ProductId <= 0 || i.WarehouseId <= 0 || !Quantity(i.Quantity) || i.UnitCost < 0 || i.UnitCost > 999999999999.999999m || decimal.Round(i.UnitCost, 6) != i.UnitCost || i.LotNumber?.Length > 100 || i.Manufactured > i.Expires ? "Revisa saldo inicial, costo, lote y motivo." : null,
        BarcodeChange b => !Operation(b.OperationId, b.Reason) || b.ProductId <= 0 || !Version(b.ExpectedVersion) || b.Code?.Length > 80 || b.Type is not ("EAN13" or "UPC" or "CODE128" or "OTRO") ? "Revisa código, tipo y versión del producto." : BarcodeValidator.Validate(b.Code, b.Type),
        CashBoxChange b => !Operation(b.OperationId, b.Reason) || b.Id < 0 || b.WarehouseId <= 0 || (b.Id != 0 && string.IsNullOrWhiteSpace(b.Code)) || b.Code?.Length > 30 || string.IsNullOrWhiteSpace(b.Name) || b.Name.Length > 100 ? "Revisa nombre, código y almacén de la caja." : null,
        OpenCashShift s => !Operation(s.OperationId, s.Reason) || s.CashBoxId <= 0 || !Money(s.OpeningAmount) ? "Revisa caja y monto de apertura." : null,
        CashMovement m => !Operation(m.OperationId, m.Reason) || m.ShiftId <= 0 || !Version(m.ExpectedVersion) || !Money(m.Amount, true) || m.Type is not ("INGRESO" or "RETIRO") ? "Revisa movimiento, versión y motivo." : null,
        CloseCashShift s => !Operation(s.OperationId, s.Reason) || s.ShiftId <= 0 || !Version(s.ExpectedVersion) || !Money(s.Counted) ? "Revisa arqueo, versión y motivo." : null,
        AccountPayment p => !Operation(p.OperationId, p.Reason) || p.AccountId <= 0 || !Version(p.ExpectedVersion) || p.MethodId <= 0 || !Money(p.Amount, true) || p.Reference?.Length > 150 || p.ShiftId is <= 0 ? "Revisa abono, referencia y versión de la cuenta." : null,
        PlaceOrder o => Order(o),
        OrderTransition t => !Operation(t.OperationId, t.Reason) || t.OrderId <= 0 || !Version(t.ExpectedVersion) || t.State is not ("RECIBIDO" or "PREPARANDO" or "LISTO" or "EN_RUTA" or "ENTREGADO" or "CANCELADO" or "INCIDENCIA") ? "Revisa estado, motivo y versión del pedido." : null,
        LinkOrderSale l => !Operation(l.OperationId, l.Reason) || l.OrderId <= 0 || l.SaleId <= 0 || !Version(l.ExpectedVersion) ? "Revisa venta y versión del pedido." : null,
        PendingFiscal f => !Operation(f.OperationId, f.Reason) || f.SaleId <= 0 || f.Type is not ("FE" or "CCFE") ? "Revisa venta y tipo fiscal pendiente." : null,
        _ => "Operación no reconocida."
    };
    private static bool Operation(Guid id, string? reason) => id != Guid.Empty && Reason(reason);
    private static bool Token(string? s) => s?.Length == 64 && s.All(Uri.IsHexDigit);
    private static string? Order(PlaceOrder o) => !Operation(o.OperationId, o.Reason) || o.CustomerId <= 0 || string.IsNullOrWhiteSpace(o.Contact) || o.Contact.Length > 150 || string.IsNullOrWhiteSpace(o.Phone) || o.Phone.Length > 30 || o.Address?.Length > 500 || (o.CustomerAddressId is null && string.IsNullOrWhiteSpace(o.Address)) || o.CustomerAddressId is <= 0 || o.Department?.Length > 100 || o.Municipality?.Length > 100 || o.Reference?.Length > 300 || o.Responsible?.Length > 150 || o.ResponsibleUserId is <= 0 || !Money(o.DeliveryCharge) || o.Items is null || o.Items.Count is < 1 or > 100 || o.Items.Any(i => i is null || i.ProductId <= 0 || !Quantity(i.Quantity)) || o.Items.Select(i => i.ProductId).Distinct().Count() != o.Items.Count ? "Revisa cliente, contacto, dirección, responsable y productos del pedido." : null;
    public static SaleCart Freeze(SaleCart c) => c with
    {
        Items = c.Items.Select(i => i with { }).OrderBy(i => i.ProductId).ToArray()
    };
}
