namespace PortfolioRebalancer.Api.Domain;

public class RebalancingEvent
{
    public Guid Id { get; private set; }
    public Guid PortfolioId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public List<RebalancingOrder> Orders { get; private set; } = new();

    private RebalancingEvent() { }

    public static RebalancingEvent Create(Guid portfolioId, IReadOnlyList<RebalancingOrder> orders)
    {
        var ev = new RebalancingEvent
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            CreatedAt = DateTimeOffset.UtcNow,
            Orders = orders.ToList(),
        };
        foreach (var order in ev.Orders)
            order.SetEventId(ev.Id);
        return ev;
    }
}
