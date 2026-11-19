using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioRebalancer.Api.Data;
using PortfolioRebalancer.Api.Domain;

namespace PortfolioRebalancer.Api.Features.Demo;

public static class DemoClaim
{
    public const string Type = "principal_kind";
    public const string Value = "demo";
    public const string PolicyName = "NotADemoToken";
}

public record StartDemoSessionCommand : IRequest<DemoSessionResponse>;

public record DemoSessionResponse(string Token, Guid PortfolioId, string UserId);

/// <summary>
/// Hands a visitor a real bearer token for a throwaway user, with a portfolio
/// already drifted out of band.
///
/// Every route on this API needs a token and nothing issued one, so there was
/// no way to show it working. A read-only bypass would have been less code and
/// would have demonstrated nothing. Minting a genuine token means the page
/// drives the same endpoints a client would, and two visitors in two tabs can
/// see that neither reaches the other's holdings.
/// </summary>
public sealed class StartDemoSessionHandler(AppDbContext db, IConfiguration config)
    : IRequestHandler<StartDemoSessionCommand, DemoSessionResponse>
{
    public const string UserPrefix = "demo-";

    // An anonymous route that writes rows is an invitation. Past the cap the
    // route refuses rather than growing the table forever.
    private int Cap => config.GetValue("DEMO_SESSION_CAP", 500);

    public async Task<DemoSessionResponse> Handle(StartDemoSessionCommand _, CancellationToken ct)
    {
        // Distinct users, not rows. Counting portfolios would mean a visitor
        // who found a way to create more could exhaust everyone else's share.
        var existing = await db.Portfolios
            .Where(p => p.UserId.StartsWith(UserPrefix))
            .Select(p => p.UserId)
            .Distinct()
            .CountAsync(ct);
        if (existing >= Cap)
        {
            throw new DomainException(
                "The demo has created as many sample portfolios as it is allowed to. " +
                "Clearing them is a maintenance task, not something a visitor can trigger.");
        }

        var userId = UserPrefix + Guid.NewGuid().ToString("N");

        var portfolio = Portfolio.Create(
            userId,
            "Sample three-fund, drifted",
            DemoData.DriftTolerancePct,
            DemoData.Allocations.Select(a => (a.Ticker, a.Weight)).ToList());

        db.Portfolios.Add(portfolio);

        foreach (var (ticker, shares, costBasis, purchased) in DemoData.Holdings)
        {
            db.Holdings.Add(Holding.Create(portfolio.Id, ticker, [(shares, costBasis, purchased)]));
        }

        await db.SaveChangesAsync(ct);

        return new DemoSessionResponse(Mint(userId), portfolio.Id, userId);
    }

    private string Mint(string userId)
    {
        var secret = config["JWT_SECRET"]
            ?? throw new InvalidOperationException("JWT_SECRET is required.");

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256);

        // Short on purpose. The token is handed to anyone who asks, so it is
        // worth only one sitting with one throwaway portfolio behind it.
        // The demo claim is what the write endpoints key off. Without it the
        // token is indistinguishable from a real one, and a visitor could
        // POST /portfolios until the cap locked everyone else out.
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(DemoClaim.Type, DemoClaim.Value),
            ],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// The sample portfolio. Equities have run and bonds have lagged, which is the
/// ordinary way a three-fund portfolio goes out of balance.
///
/// VNQ is left inside the tolerance band on purpose: a portfolio where every
/// line is out of band does not show that the band does anything.
/// </summary>
public static class DemoData
{
    public const decimal DriftTolerancePct = 5.0m;

    public static readonly (string Ticker, decimal Weight)[] Allocations =
    [
        ("VTI", 0.40m),
        ("VXUS", 0.20m),
        ("BND", 0.30m),
        ("VNQ", 0.10m),
    ];

    public static readonly (string Ticker, decimal Shares, decimal CostBasis, DateOnly Purchased)[] Holdings =
    [
        ("VTI", 160m, 198.40m, new DateOnly(2023, 3, 14)),
        ("VXUS", 180m, 55.90m, new DateOnly(2023, 3, 14)),
        ("BND", 220m, 76.15m, new DateOnly(2023, 6, 2)),
        ("VNQ", 95m, 81.30m, new DateOnly(2024, 1, 18)),
    ];

    // Static rather than fetched. A live quote feed would make the demo depend
    // on a vendor, a key and a budget, and the arithmetic being shown does not
    // get more convincing for the prices being today's.
    public static readonly IReadOnlyDictionary<string, decimal> Prices =
        new Dictionary<string, decimal>
        {
            ["VTI"] = 268.40m,
            ["VXUS"] = 62.15m,
            ["BND"] = 73.80m,
            ["VNQ"] = 88.25m,
        };
}
