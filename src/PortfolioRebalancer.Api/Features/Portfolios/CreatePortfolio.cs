using MediatR;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Portfolios;

public record AllocationInput(string Ticker, decimal Weight);

// UserId is injected by the endpoint from the validated JWT claim.
public record CreatePortfolioCommand(
    string UserId,
    string Name,
    decimal DriftTolerancePct,
    IReadOnlyList<AllocationInput> Allocations
) : IRequest<PortfolioResponse>;

public record PortfolioResponse(
    Guid Id,
    string Name,
    decimal DriftTolerancePct,
    IReadOnlyList<AllocationInput> Allocations,
    DateTimeOffset CreatedAt
);

public sealed class CreatePortfolioHandler(AppDbContext db) : IRequestHandler<CreatePortfolioCommand, PortfolioResponse>
{
    public async Task<PortfolioResponse> Handle(CreatePortfolioCommand cmd, CancellationToken ct)
    {
        var allocations = cmd.Allocations.Select(a => (a.Ticker, a.Weight)).ToList();
        var portfolio = Portfolio.Create(cmd.UserId, cmd.Name, cmd.DriftTolerancePct, allocations);

        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync(ct);

        return portfolio.ToResponse();
    }
}
