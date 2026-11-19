using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioRebalancer.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PortfolioRebalancer.Tests.Api;

/// <summary>
/// Every route needs a bearer token and nothing issued one, so the API could
/// not be shown to anyone. The demo session mints one for a throwaway user and
/// seeds it a portfolio that is already out of band.
///
/// It hands out a real token for the real endpoints on purpose. A read-only
/// bypass would have been less work and would have demonstrated nothing: the
/// thing worth showing is that two visitors cannot see each other's holdings.
/// </summary>
public class DemoSessionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));
                });
                host.UseSetting("JWT_SECRET", TestJwt.Secret);
                host.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }

    private static HttpClient Authed(HttpClient c, string token)
    {
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<(string Token, Guid PortfolioId)> StartSession()
    {
        var res = await _client.PostAsync("/api/demo/session", null);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("token").GetString()!, body.GetProperty("portfolioId").GetGuid());
    }

    [Fact]
    public async Task DemoSessionIsAnonymous()
    {
        // The one route that cannot require a token, since it is where tokens
        // come from.
        var res = await _client.PostAsync("/api/demo/session", null);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DemoTokenOpensTheRealEndpoints()
    {
        var (token, portfolioId) = await StartSession();
        var client = Authed(_factory.CreateClient(), token);

        var res = await client.GetAsync($"/api/portfolios/{portfolioId}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoVisitorsCannotSeeEachOthersPortfolios()
    {
        // The authorization claim this repo makes: every query filters by the
        // caller's user id at the EF level. Two demo sessions is the cheapest
        // way to put that in front of someone.
        var (tokenA, portfolioA) = await StartSession();
        var (tokenB, _) = await StartSession();

        var asB = Authed(_factory.CreateClient(), tokenB);
        var res = await asB.GetAsync($"/api/portfolios/{portfolioA}");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EachSessionGetsItsOwnUser()
    {
        var (_, portfolioA) = await StartSession();
        var (_, portfolioB) = await StartSession();

        portfolioA.Should().NotBe(portfolioB);
    }

    [Fact]
    public async Task TheSeededPortfolioIsAlreadyOutOfBand()
    {
        // A portfolio sitting exactly on target renders a screen of zeroes and
        // shows nothing. The drift has to be there before the visitor arrives.
        var (token, portfolioId) = await StartSession();
        var client = Authed(_factory.CreateClient(), token);

        var prices = await client.GetFromJsonAsync<JsonElement>("/api/demo/prices");
        var res = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/drift", new { prices });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await res.Content.ReadFromJsonAsync<JsonElement>();
        var positions = report.GetProperty("positions").EnumerateArray().ToList();

        positions.Should().NotBeEmpty();
        positions.Should().Contain(p => p.GetProperty("isOutOfBand").GetBoolean(),
            "a demo portfolio with nothing out of band gives the rebalance nothing to do");
    }

    [Fact]
    public async Task TheSeededPortfolioRebalancesIntoSellsBeforeBuys()
    {
        // The ordering is the domain claim: proceeds from sells fund the buys.
        var (token, portfolioId) = await StartSession();
        var client = Authed(_factory.CreateClient(), token);

        var prices = await client.GetFromJsonAsync<JsonElement>("/api/demo/prices");
        var res = await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/rebalance", new { prices });
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var orders = (await res.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("orders").EnumerateArray().ToList();

        orders.Should().NotBeEmpty();
        var lastSell = orders.FindLastIndex(o => o.GetProperty("action").GetString() == "Sell");
        var firstBuy = orders.FindIndex(o => o.GetProperty("action").GetString() == "Buy");
        if (lastSell >= 0 && firstBuy >= 0)
        {
            lastSell.Should().BeLessThan(firstBuy, "sells fund the buys, so they come first");
        }
    }

    [Fact]
    public async Task DriftIsReportedInPointsNotFractions()
    {
        // targetWeight and actualWeight are fractions; driftPct is already in
        // percentage points. Treating them alike rendered "over by 1453.1%".
        var (token, portfolioId) = await StartSession();
        var client = Authed(_factory.CreateClient(), token);

        var prices = await client.GetFromJsonAsync<JsonElement>("/api/demo/prices");
        var report = await (await client.PostAsJsonAsync($"/api/portfolios/{portfolioId}/drift", new { prices }))
            .Content.ReadFromJsonAsync<JsonElement>();

        foreach (var p in report.GetProperty("positions").EnumerateArray())
        {
            var target = p.GetProperty("targetWeight").GetDecimal();
            var actual = p.GetProperty("actualWeight").GetDecimal();
            var drift = p.GetProperty("driftPct").GetDecimal();

            target.Should().BeLessThanOrEqualTo(1m, "weights are fractions");
            drift.Should().BeApproximately((actual - target) * 100m, 0.01m,
                "driftPct is the gap in percentage points");
        }
    }

    [Fact]
    public async Task TheRootServesThePage()
    {
        var res = await _client.GetAsync("/");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }

    [Fact]
    public async Task ThePageDrivesTheRoutesThatExist()
    {
        // The page reads these field names off the responses. A rename on
        // either side empties a column with nothing failing.
        var body = await _client.GetStringAsync("/");

        body.Should().Contain("/api/demo/session");
        body.Should().Contain("/api/demo/prices");
        body.Should().Contain("/drift");
        body.Should().Contain("/rebalance");

        foreach (var field in new[] { "totalPortfolioValue", "positions", "targetWeight",
                                      "actualWeight", "driftPct", "isOutOfBand", "marketValue",
                                      "orders", "estimatedValue", "driftTolerancePct" })
        {
            body.Should().Contain(field, $"the page reads {field}");
        }
    }

    [Fact]
    public async Task DemoSessionsAreCappedSoTheTableCannotBeFilled()
    {
        // An anonymous route that writes rows is an invitation. Asserting that
        // six requests make at most six portfolios would pass with no cap at
        // all, so this stands up a factory with the cap set to three and
        // watches the fourth be refused.
        using var capped = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));
                });
                host.UseSetting("JWT_SECRET", TestJwt.Secret);
                host.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
                host.UseSetting("DEMO_SESSION_CAP", "3");
            });

        using var scope = capped.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alreadyThere = await db.Portfolios.CountAsync(p => p.UserId.StartsWith("demo-"));

        var client = capped.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.PostAsync("/api/demo/session", null)).StatusCode);
        }

        var created = await db.Portfolios.CountAsync(p => p.UserId.StartsWith("demo-")) - alreadyThere;
        created.Should().BeLessThan(5, "the cap has to actually stop something");
        statuses.Should().Contain(s => s != HttpStatusCode.Created,
            "past the cap the route refuses rather than growing the table");
    }
}
