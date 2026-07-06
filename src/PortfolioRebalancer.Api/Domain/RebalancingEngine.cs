namespace PortfolioRebalancer.Api.Domain;

public static class RebalancingEngine
{
    /// <summary>
    /// Generates buy/sell orders to bring all out-of-band positions back to target.
    /// Sells are generated before buys so proceeds can fund purchases.
    /// </summary>
    public static IReadOnlyList<RebalancingOrder> GenerateOrders(
        DriftReport drift,
        IReadOnlyDictionary<string, decimal> pricesByTicker)
    {
        var outOfBand = drift.Positions.Where(p => p.IsOutOfBand).ToList();
        if (outOfBand.Count == 0) return Array.Empty<RebalancingOrder>();

        var sells = new List<RebalancingOrder>();
        var buys = new List<RebalancingOrder>();

        foreach (var position in outOfBand)
        {
            if (!pricesByTicker.TryGetValue(position.Ticker, out var price) || price <= 0)
                throw new DomainException($"Missing or invalid price for ticker '{position.Ticker}'.");

            var targetValue = position.TargetWeight * drift.TotalPortfolioValue;
            var delta = targetValue - position.MarketValue;
            var shares = Math.Abs(delta) / price;

            if (delta < 0)
                sells.Add(RebalancingOrder.Create(position.Ticker, OrderAction.Sell, shares, Math.Abs(delta)));
            else
                buys.Add(RebalancingOrder.Create(position.Ticker, OrderAction.Buy, shares, delta));
        }

        return [.. sells, .. buys];
    }
}
