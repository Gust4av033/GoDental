namespace GoDental.Domain.Rules;
public static class ImportRules
{
    public static bool CanMove(string state, string next) => state == next || (state, next) is
        ("CONFIRMADA", "EN_PRODUCCION" or "DESPACHADA") or ("EN_PRODUCCION", "DESPACHADA")
        or ("DESPACHADA", "EN_TRANSITO" or "ADUANA") or ("EN_TRANSITO", "ADUANA");
    public static IReadOnlyDictionary<long, decimal> Allocate(decimal total, IReadOnlyList<(long Id, decimal Weight)> weights)
    {
        if (total < 0 || decimal.Round(total, 2) != total || weights.Count == 0 || weights.Any(w => w.Weight < 0) || weights.Select(w => w.Id).Distinct().Count() != weights.Count)
            throw new ArgumentException("Revisa importe y bases del reparto.");
        var sum = weights.Sum(w => w.Weight);
        if (sum == 0 && total > 0) throw new ArgumentException("No se pueden distribuir gastos sobre una compra de valor neto cero.");
        var cents = total * 100;
        var quotas = weights.Select(w => (w.Id, Value: sum == 0 ? 0 : cents * (w.Weight / sum))).ToArray();
        var allocations = quotas.ToDictionary(w => w.Id, w => decimal.Floor(w.Value));
        var residual = cents - allocations.Values.Sum();
        if (residual < 0 || residual > weights.Count) throw new ArgumentException("El reparto excede la precisión admitida.");
        foreach (var q in quotas.OrderByDescending(w => w.Value - decimal.Floor(w.Value)).ThenBy(w => w.Id).Take((int)residual)) allocations[q.Id]++;
        return allocations.ToDictionary(w => w.Key, w => w.Value / 100);
    }
    public static decimal PartialExpense(decimal budget, decimal ordered, decimal received, decimal current, decimal spent)
    {
        if (ordered <= 0 || received < 0 || current <= 0 || received + current > ordered || spent < 0 || spent > budget) throw new ArgumentException("Revisa cantidades y presupuesto recibido.");
        var target = received + current == ordered ? budget : PurchaseRules.Money(budget * ((received + current) / ordered));
        if (target < spent) throw new ArgumentException("El gasto aplicado no concuerda con el presupuesto.");
        return target - spent;
    }
}
