namespace PortfolioRebalancer.Api.Domain;

public class Portfolio
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    // Drift threshold in percentage points, e.g. 5.0 means ±5% before rebalancing triggers.
    public decimal DriftTolerancePct { get; private set; }

    public List<TargetAllocation> TargetAllocations { get; private set; } = new();
    public List<Holding> Holdings { get; private set; } = new();
    public List<RebalancingEvent> RebalancingEvents { get; private set; } = new();
    public DateTimeOffset CreatedAt { get; private set; }

    private Portfolio() { }

    public static Portfolio Create(string userId, string name, decimal driftTolerancePct, IReadOnlyList<(string ticker, decimal weight)> allocations)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new DomainException("UserId is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Portfolio name is required.");
        if (driftTolerancePct is < 0.1m or > 50m) throw new DomainException("DriftTolerancePct must be between 0.1 and 50.");
        if (allocations.Count == 0) throw new DomainException("At least one target allocation is required.");

        var weightSum = allocations.Sum(a => a.weight);
        if (Math.Abs(weightSum - 1.0m) > 0.001m)
            throw new DomainException($"Target allocations must sum to 1.0 (got {weightSum:F4}).");

        var id = Guid.NewGuid();
        return new Portfolio
        {
            Id = id,
            UserId = userId,
            Name = name,
            DriftTolerancePct = driftTolerancePct,
            TargetAllocations = allocations.Select(a => TargetAllocation.Create(id, a.ticker, a.weight)).ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
