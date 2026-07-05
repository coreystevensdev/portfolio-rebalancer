using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Rebalancing;

public record GetDriftQuery(
    string UserId,
    Guid PortfolioId,
    IReadOnlyDictionary<string, decimal> PricesByTicker
) : IRequest<DriftResponse>;

public record DriftPositionDto(
    string Ticker,
    decimal TargetWeight,
    decimal ActualWeight,
    decimal DriftPct,
    bool IsOutOfBand,
    decimal Shares,
    decimal MarketValue
);

public record DriftResponse(
    decimal TotalPortfolioValue,
    bool AnyOutOfBand,
    IReadOnlyList<DriftPositionDto> Positions
);

public sealed class GetDriftHandler(AppDbContext db) : IRequestHandler<GetDriftQuery, DriftResponse>
{
    public async Task<DriftResponse> Handle(GetDriftQuery query, CancellationToken ct)
    {
        var portfolio = await db.Portfolios
            .Include(p => p.TargetAllocations)
            .Include(p => p.Holdings).ThenInclude(h => h.Lots)
            .Where(p => p.Id == query.PortfolioId && p.UserId == query.UserId)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Portfolio not found.");

        var report = DriftCalculator.Calculate(portfolio, query.PricesByTicker);

        return new DriftResponse(
            report.TotalPortfolioValue,
            report.Positions.Any(p => p.IsOutOfBand),
            report.Positions.Select(p => new DriftPositionDto(
                p.Ticker, p.TargetWeight, p.ActualWeight, p.DriftPct, p.IsOutOfBand, p.Shares, p.MarketValue
            )).ToList()
        );
    }
}
