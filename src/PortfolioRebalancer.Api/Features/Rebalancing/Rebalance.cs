using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Rebalancing;

public record RebalanceCommand(
    string UserId,
    Guid PortfolioId,
    IReadOnlyDictionary<string, decimal> PricesByTicker
) : IRequest<RebalanceResponse>;

public record OrderDto(string Ticker, string Action, decimal Shares, decimal EstimatedValue);

public record RebalanceResponse(
    Guid EventId,
    DateTimeOffset CreatedAt,
    decimal TotalPortfolioValue,
    IReadOnlyList<OrderDto> Orders
);

public sealed class RebalanceHandler(AppDbContext db) : IRequestHandler<RebalanceCommand, RebalanceResponse>
{
    public async Task<RebalanceResponse> Handle(RebalanceCommand cmd, CancellationToken ct)
    {
        var portfolio = await db.Portfolios
            .Include(p => p.TargetAllocations)
            .Include(p => p.Holdings).ThenInclude(h => h.Lots)
            .Where(p => p.Id == cmd.PortfolioId && p.UserId == cmd.UserId)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Portfolio not found.");

        var drift = DriftCalculator.Calculate(portfolio, cmd.PricesByTicker);
        var orders = RebalancingEngine.GenerateOrders(drift, cmd.PricesByTicker);

        var ev = RebalancingEvent.Create(cmd.PortfolioId, orders);
        db.RebalancingEvents.Add(ev);
        await db.SaveChangesAsync(ct);

        return new RebalanceResponse(
            ev.Id,
            ev.CreatedAt,
            drift.TotalPortfolioValue,
            ev.Orders.Select(o => new OrderDto(o.Ticker, o.Action.ToString(), o.Shares, o.EstimatedValue)).ToList()
        );
    }
}
