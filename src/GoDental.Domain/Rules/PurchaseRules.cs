namespace GoDental.Domain.Rules;

public static class PurchaseRules
{
    public static bool CanReceive(string state) => state is "CONFIRMADA" or "EN_PRODUCCION" or "DESPACHADA" or "RECIBIDA_PARCIAL";
    public static bool CanTransition(string state, string next, bool received) => next == "CANCELADA"
        ? !received && state is "BORRADOR" or "ENVIADA" or "CONFIRMADA" or "EN_PRODUCCION" or "DESPACHADA"
        : (state, next) is ("BORRADOR", "ENVIADA" or "CONFIRMADA") or ("ENVIADA", "CONFIRMADA")
            or ("CONFIRMADA", "EN_PRODUCCION" or "DESPACHADA") or ("EN_PRODUCCION", "DESPACHADA");
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static decimal Cost(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    public static decimal PurchaseCostUsd(decimal cost, decimal discount, decimal ordered, decimal exchangeRate)
        => Cost((cost - discount / ordered) * exchangeRate);
    public static decimal Average(decimal stock, decimal previousCost, decimal received, decimal cost)
        => Cost((stock * previousCost + received * cost) / (stock + received));
}
