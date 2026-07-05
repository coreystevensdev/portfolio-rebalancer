namespace PortfolioRebalancer.Api.Domain;

public class Holding
{
    public Guid Id { get; private set; }
    public Guid PortfolioId { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public List<Lot> Lots { get; private set; } = new();

    public decimal TotalShares => Lots.Sum(l => l.Shares);

    private Holding() { }

    public static Holding Create(Guid portfolioId, string ticker, IReadOnlyList<(decimal shares, decimal costBasisPerShare, DateOnly purchasedAt)> lots)
    {
        if (string.IsNullOrWhiteSpace(ticker)) throw new DomainException("Ticker is required.");
        if (lots.Count == 0) throw new DomainException("At least one lot is required.");

        var holdingId = Guid.NewGuid();
        return new Holding
        {
            Id = holdingId,
            PortfolioId = portfolioId,
            Ticker = ticker.ToUpperInvariant(),
            Lots = lots.Select(l => Lot.Create(holdingId, l.shares, l.costBasisPerShare, l.purchasedAt)).ToList(),
        };
    }
}
