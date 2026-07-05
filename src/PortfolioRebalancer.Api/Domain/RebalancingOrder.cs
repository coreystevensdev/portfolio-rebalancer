namespace PortfolioRebalancer.Api.Domain;

public enum OrderAction { Buy, Sell }

public class RebalancingOrder
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public OrderAction Action { get; private set; }
    public decimal Shares { get; private set; }
    public decimal EstimatedValue { get; private set; }

    private RebalancingOrder() { }

    public static RebalancingOrder Create(string ticker, OrderAction action, decimal shares, decimal estimatedValue)
    {
        return new RebalancingOrder
        {
            Id = Guid.NewGuid(),
            Ticker = ticker.ToUpperInvariant(),
            Action = action,
            Shares = Math.Round(shares, 6),
            EstimatedValue = Math.Round(estimatedValue, 2),
        };
    }

    // Called by RebalancingEvent.Create once the parent event id is known.
    internal void SetEventId(Guid eventId) => EventId = eventId;
}
