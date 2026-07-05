namespace PortfolioRebalancer.Api.Domain;

public class TargetAllocation
{
    public Guid Id { get; private set; }
    public Guid PortfolioId { get; private set; }
    public string Ticker { get; private set; } = string.Empty;

    // Fractional weight, 0.0-1.0. All allocations in a portfolio must sum to 1.0.
    public decimal TargetWeight { get; private set; }

    private TargetAllocation() { }

    internal static TargetAllocation Create(Guid portfolioId, string ticker, decimal weight)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new DomainException("Ticker is required.");
        if (weight is <= 0m or > 1m) throw new DomainException($"Weight for {ticker} must be between 0 and 1.");
        return new TargetAllocation { Id = Guid.NewGuid(), PortfolioId = portfolioId, Ticker = ticker.ToUpperInvariant(), TargetWeight = weight };
    }
}
