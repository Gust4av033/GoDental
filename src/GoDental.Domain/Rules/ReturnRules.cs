namespace GoDental.Domain.Rules;

public static class ReturnRules
{
    public static bool WithinWindow(DateTimeOffset purchased, DateTimeOffset requested, int hours)
        => hours > 0 && requested >= purchased && (requested - purchased).TotalHours <= hours;

    // Diferencia acumulada: conserva exactamente los centavos al terminar las parciales.
    public static decimal Amount(decimal sold, decimal total, decimal returned, decimal refunded, decimal quantity)
    {
        if (sold <= 0 || total < 0 || returned < 0 || quantity <= 0 || returned + quantity > sold || refunded < 0 || refunded > total)
            throw new ArgumentException("La cantidad supera el saldo de la línea original.");
        var target = returned + quantity == sold ? total : decimal.Round(total * ((returned + quantity) / sold), 2, MidpointRounding.AwayFromZero);
        if (target < refunded) throw new ArgumentException("El historial de reintegros no concilia con la venta.");
        return target - refunded;
    }

    public static (decimal DebtReduction, decimal CashRefund) Settlement(decimal amount, decimal pending)
    {
        if (amount < 0 || pending < 0) throw new ArgumentException("Revisa los importes del reintegro.");
        var debt = Math.Min(amount, pending);
        return (debt, amount - debt);
    }
}
