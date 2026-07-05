using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Portfolios;

public record LotInput(decimal Shares, decimal CostBasisPerShare, DateOnly PurchasedAt);

public record AddHoldingCommand(
    string UserId,
    Guid PortfolioId,
    string Ticker,
    IReadOnlyList<LotInput> Lots
) : IRequest<AddHoldingResponse>;

public record AddHoldingResponse(Guid HoldingId, string Ticker, decimal TotalShares);

public sealed class AddHoldingHandler(AppDbContext db) : IRequestHandler<AddHoldingCommand, AddHoldingResponse>
{
    public async Task<AddHoldingResponse> Handle(AddHoldingCommand cmd, CancellationToken ct)
    {
        var portfolio = await db.Portfolios
            .Include(p => p.Holdings)
            .Where(p => p.Id == cmd.PortfolioId && p.UserId == cmd.UserId)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Portfolio not found.");

        var lots = cmd.Lots.Select(l => (l.Shares, l.CostBasisPerShare, l.PurchasedAt)).ToList();
        var holding = Holding.Create(cmd.PortfolioId, cmd.Ticker, lots);

        db.Holdings.Add(holding);
        await db.SaveChangesAsync(ct);

        return new AddHoldingResponse(holding.Id, holding.Ticker, holding.TotalShares);
    }
}
