namespace GoDental.Domain.Rules;

public static class InventoryCountRules
{
    public const decimal MaximumQuantity = 99999999999999.9999m;

    public static bool IsValidQuantity(decimal quantity) =>
        quantity >= 0 && quantity <= MaximumQuantity && decimal.Round(quantity, 4) == quantity;

    public static decimal Difference(decimal systemQuantity, decimal countedQuantity)
    {
        if (!IsValidQuantity(systemQuantity) || !IsValidQuantity(countedQuantity))
            throw new ArgumentOutOfRangeException(nameof(countedQuantity), "Las cantidades admiten hasta cuatro decimales y no pueden ser negativas.");
        return countedQuantity - systemQuantity;
    }
}
