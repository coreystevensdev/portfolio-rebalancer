using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Portfolios;

internal static class PortfolioExtensions
{
    internal static PortfolioResponse ToResponse(this Portfolio p) =>
        new(
            p.Id,
            p.Name,
            p.DriftTolerancePct,
            p.TargetAllocations.Select(a => new AllocationInput(a.Ticker, a.TargetWeight)).ToList(),
            p.CreatedAt
        );
}
