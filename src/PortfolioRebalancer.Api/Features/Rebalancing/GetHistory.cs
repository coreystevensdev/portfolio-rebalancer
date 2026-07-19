using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;

namespace PortfolioRebalancer.Api.Features.Rebalancing;

public record GetHistoryQuery(string UserId, Guid PortfolioId) : IRequest<IReadOnlyList<RebalanceResponse>>;

public sealed class GetHistoryHandler(AppDbContext db) : IRequestHandler<GetHistoryQuery, IReadOnlyList<RebalanceResponse>>
{
    public async Task<IReadOnlyList<RebalanceResponse>> Handle(GetHistoryQuery query, CancellationToken ct)
    {
        var events = await db.RebalancingEvents
            .Include(ev => ev.Orders)
            .Where(ev => ev.PortfolioId == query.PortfolioId
                && db.Portfolios.Any(p => p.Id == ev.PortfolioId && p.UserId == query.UserId))
            .OrderByDescending(ev => ev.CreatedAt)
            .ToListAsync(ct);

        return events.Select(ev => new RebalanceResponse(
            ev.Id,
            ev.CreatedAt,
            TotalPortfolioValue: null, // not stored; drift is recalculated live, not retroactively
            ev.Orders.Select(o => new OrderDto(o.Ticker, o.Action.ToString(), o.Shares, o.EstimatedValue)).ToList()
        )).ToList();
    }
}
