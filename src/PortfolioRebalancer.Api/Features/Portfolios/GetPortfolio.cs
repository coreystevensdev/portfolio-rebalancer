using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Portfolios;

public record GetPortfolioQuery(string UserId, Guid PortfolioId) : IRequest<PortfolioResponse?>;

public sealed class GetPortfolioHandler(AppDbContext db) : IRequestHandler<GetPortfolioQuery, PortfolioResponse?>
{
    public async Task<PortfolioResponse?> Handle(GetPortfolioQuery query, CancellationToken ct)
    {
        var portfolio = await db.Portfolios
            .Include(p => p.TargetAllocations)
            .Include(p => p.Holdings).ThenInclude(h => h.Lots)
            .Where(p => p.Id == query.PortfolioId && p.UserId == query.UserId)
            .FirstOrDefaultAsync(ct);

        return portfolio?.ToResponse();
    }
}
