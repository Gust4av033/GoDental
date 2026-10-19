using GoDental.Application.DTOs;

namespace GoDental.Application.Services;
public sealed record PriceChoice(int Id, decimal Price, int Priority, decimal MinimumQuantity);
public sealed record PromotionChoice(long Id, string Type, decimal? FixedPrice, decimal? Percent, bool Accumulable);
public sealed record PricingInput(int ProductId, string Sku, string Name, decimal Quantity, decimal PublicPrice, decimal LineDiscount, decimal GlobalDiscount, IReadOnlyList<PriceChoice> Prices, IReadOnlyList<PromotionChoice> Promotions, decimal TaxRate, bool TaxIncluded, IReadOnlyList<LotAllocation> Lots);
public static class CommercialPricing
{
    public static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static decimal EffectiveDiscount(decimal line, decimal global) => 100m * (1m - (1m - line / 100m) * (1m - global / 100m));
    public static CalculatedPrice Calculate(PricingInput p)
    {
        if (p.Quantity <= 0 || p.PublicPrice < 0 || p.LineDiscount is < 0 or > 100 || p.GlobalDiscount is < 0 or > 100 || p.TaxRate < 0 || p.TaxRate > 100)
            throw new ArgumentException("Revisa cantidades, precios, descuentos e impuesto.");
        if (p.Prices.Any(x => x.Price < 0 || x.MinimumQuantity <= 0))
            throw new ArgumentException("La lista contiene un precio o cantidad mínima inválidos.");
        var list = p.Prices.Where(x => x.MinimumQuantity <= p.Quantity).OrderByDescending(x => x.Priority).ThenByDescending(x => x.MinimumQuantity).ThenBy(x => x.Id).FirstOrDefault();
        var customerPrice = list?.Price ?? p.PublicPrice;
        decimal price = customerPrice;
        long? promoId = null;
        foreach (var promo in p.Promotions.OrderBy(x => x.Id))
        {
            if (promo.Type is not ("PORCENTAJE" or "PRECIO_FIJO"))
                throw new ArgumentException("La promoción aplicable requiere una modalidad todavía no soportada.");
            decimal candidate;
            if (promo.Type == "PRECIO_FIJO" && promo.FixedPrice is >= 0)
                candidate = Money(promo.FixedPrice.Value);
            else if (promo.Type == "PORCENTAJE" && promo.Percent is >= 0 and <= 100)
                candidate = Money((promo.Accumulable ? customerPrice : p.PublicPrice) * (1 - promo.Percent.Value / 100));
            else
                throw new ArgumentException("La promoción aplicable no tiene un precio o porcentaje válido.");
            if (candidate < price)
            {
                price = candidate;
                promoId = promo.Id;
            }
        }

        return Build(p, price, list?.Id, promoId);
    }

    public static CalculatedPrice ForAllocation(PricingInput input, CalculatedPrice selected, LotAllocation allocation) => Build(input with { Quantity = allocation.Quantity, Lots = [allocation] }, selected.UnitPrice, selected.PriceListId, selected.PromotionId);
    private static CalculatedPrice Build(PricingInput p, decimal price, int? listId, long? promoId)
    {
        if (price > 0 && p.Quantity > 9999999999999999.99m / price)
            throw new ArgumentException("El importe de la línea supera el rango monetario.");
        var effective = EffectiveDiscount(p.LineDiscount, p.GlobalDiscount);
        var before = Money(price * p.Quantity);
        var after = before - Money(before * effective / 100);
        var subtotal = p.TaxIncluded ? Money(before / (1 + p.TaxRate / 100)) : before;
        var netAfter = p.TaxIncluded ? Money(after / (1 + p.TaxRate / 100)) : after;
        var tax = p.TaxIncluded ? after - netAfter : Money(netAfter * p.TaxRate / 100);
        return new(p.ProductId, p.Sku, p.Name, p.Quantity, p.PublicPrice, price, p.TaxIncluded ? Money(price / (1 + p.TaxRate / 100)) : price, subtotal, subtotal - netAfter, tax, netAfter + tax, effective, listId, promoId, p.Lots);
    }

    public static IReadOnlyList<LotAllocation> Allocate(decimal quantity, IReadOnlyList<(long Id, decimal Available)> lots)
    {
        if (quantity <= 0 || lots.Any(l => l.Id <= 0 || l.Available < 0) || lots.Select(l => l.Id).Distinct().Count() != lots.Count)
            throw new ArgumentException("Revisa cantidad y saldos de los lotes.");
        var result = new List<LotAllocation>();
        var remaining = quantity;
        foreach (var l in lots)
        {
            var take = Math.Min(remaining, l.Available);
            if (take > 0)
            {
                result.Add(new(l.Id, take));
                remaining -= take;
            }

            if (remaining == 0)
                break;
        }

        if (remaining > 0)
            throw new ArgumentException("Los lotes vendibles no tienen cantidad suficiente.");
        return result;
    }
}
