using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Rebalancing;

public record GetHistoryQuery(string UserId, Guid PortfolioId) : IRequest<IReadOnlyList<RebalanceResponse>>;

public sealed class GetHistoryHandler(AppDbContext db) : IRequestHandler<GetHistoryQuery, IReadOnlyList<RebalanceResponse>>
{
    public async Task<IReadOnlyList<RebalanceResponse>> Handle(GetHistoryQuery query, CancellationToken ct)
    {
        var portfolioExists = await db.Portfolios
            .AnyAsync(p => p.Id == query.PortfolioId && p.UserId == query.UserId, ct);

        if (!portfolioExists) throw new DomainException("Portfolio not found.");

        var events = await db.RebalancingEvents
            .Include(ev => ev.Orders)
            .Where(ev => ev.PortfolioId == query.PortfolioId)
            .OrderByDescending(ev => ev.CreatedAt)
            .ToListAsync(ct);

        return events.Select(ev => new RebalanceResponse(
            ev.Id,
            ev.CreatedAt,
            TotalPortfolioValue: 0m, // not stored; drift is recalculated live
            ev.Orders.Select(o => new OrderDto(o.Ticker, o.Action.ToString(), o.Shares, o.EstimatedValue)).ToList()
        )).ToList();
    }
}
