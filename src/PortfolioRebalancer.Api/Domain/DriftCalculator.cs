namespace PortfolioRebalancer.Api.Domain;

public record DriftPosition(
    string Ticker,
    decimal TargetWeight,
    decimal ActualWeight,
    decimal DriftPct,
    bool IsOutOfBand,
    decimal Shares,
    decimal MarketValue
);

public record DriftReport(
    decimal TotalPortfolioValue,
    IReadOnlyList<DriftPosition> Positions
);

public static class DriftCalculator
{
    public static DriftReport Calculate(Portfolio portfolio, IReadOnlyDictionary<string, decimal> pricesByTicker)
    {
        var holdingsByTicker = portfolio.Holdings.ToDictionary(h => h.Ticker, h => h);

        var marketValues = portfolio.TargetAllocations
            .ToDictionary(
                a => a.Ticker,
                a => (holdingsByTicker.TryGetValue(a.Ticker, out var h) ? h.TotalShares : 0m)
                    * GetPrice(pricesByTicker, a.Ticker)
            );

        var totalValue = marketValues.Values.Sum();

        var positions = portfolio.TargetAllocations.Select(alloc =>
        {
            var marketValue = marketValues[alloc.Ticker];
            var actualWeight = totalValue > 0 ? marketValue / totalValue : 0m;
            var driftPct = (actualWeight - alloc.TargetWeight) * 100m;
            var shares = holdingsByTicker.TryGetValue(alloc.Ticker, out var h) ? h.TotalShares : 0m;

            return new DriftPosition(
                Ticker: alloc.Ticker,
                TargetWeight: alloc.TargetWeight,
                ActualWeight: Math.Round(actualWeight, 6),
                DriftPct: Math.Round(driftPct, 4),
                IsOutOfBand: Math.Abs(driftPct) > portfolio.DriftTolerancePct,
                Shares: shares,
                MarketValue: Math.Round(marketValue, 2)
            );
        }).ToList();

        return new DriftReport(Math.Round(totalValue, 2), positions);
    }

    private static decimal GetPrice(IReadOnlyDictionary<string, decimal> prices, string ticker)
    {
        if (!prices.TryGetValue(ticker, out var price) || price <= 0)
            throw new DomainException($"Missing or invalid price for ticker '{ticker}'.");
        return price;
    }
}
