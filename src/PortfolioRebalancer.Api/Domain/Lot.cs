namespace PortfolioRebalancer.Api.Domain;

public class Lot
{
    public Guid Id { get; private set; }
    public Guid HoldingId { get; private set; }
    public decimal Shares { get; private set; }
    public decimal CostBasisPerShare { get; private set; }
    public DateOnly PurchasedAt { get; private set; }

    private Lot() { }

    internal static Lot Create(Guid holdingId, decimal shares, decimal costBasisPerShare, DateOnly purchasedAt)
    {
        if (shares <= 0) throw new DomainException("Shares must be positive.");
        if (costBasisPerShare <= 0) throw new DomainException("CostBasisPerShare must be positive.");
        return new Lot
        {
            Id = Guid.NewGuid(),
            HoldingId = holdingId,
            Shares = shares,
            CostBasisPerShare = costBasisPerShare,
            PurchasedAt = purchasedAt,
        };
    }
}
