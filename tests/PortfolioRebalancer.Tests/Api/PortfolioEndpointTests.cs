using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioRebalancer.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PortfolioRebalancer.Tests.Api;

public class PortfolioEndpointTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

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
                    // Replace EF context with the TestContainers connection string.
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_postgres.GetConnectionString()));
                });

                host.UseSetting("JWT_SECRET", TestJwt.Secret);
                host.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            });

        // Apply migrations.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Generate());
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task HealthEndpointReturns200()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreatePortfolio_Returns201()
    {
        var body = new
        {
            name = "Tech Heavy",
            driftTolerancePct = 5.0m,
            allocations = new[] { new { ticker = "AAPL", weight = 0.6 }, new { ticker = "MSFT", weight = 0.4 } }
        };

        var response = await _client.PostAsJsonAsync("/api/portfolios", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePortfolio_Returns400WhenAllocationsDoNotSumToOne()
    {
        var body = new
        {
            name = "Bad Portfolio",
            driftTolerancePct = 5.0m,
            allocations = new[] { new { ticker = "AAPL", weight = 0.5 }, new { ticker = "MSFT", weight = 0.3 } }
        };

        var response = await _client.PostAsJsonAsync("/api/portfolios", body);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPortfolio_Returns404ForUnknownId()
    {
        var response = await _client.GetAsync($"/api/portfolios/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPortfolio_Returns401WhenNotAuthenticated()
    {
        using var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync($"/api/portfolios/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateThenGetPortfolio_RoundTrips()
    {
        var createBody = new
        {
            name = "Index Fund",
            driftTolerancePct = 3.0m,
            allocations = new[] { new { ticker = "VTI", weight = 0.7 }, new { ticker = "BND", weight = 0.3 } }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/portfolios", createBody);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var locationHeader = createResponse.Headers.Location?.ToString();
        locationHeader.Should().NotBeNull();

        var getResponse = await _client.GetAsync(locationHeader);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DriftEndpoint_Returns200()
    {
        var createBody = new
        {
            name = "Drift Test",
            driftTolerancePct = 5.0m,
            allocations = new[] { new { ticker = "SPY", weight = 1.0 } }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/portfolios", createBody);
        var locationHeader = createResponse.Headers.Location!.ToString();
        var id = locationHeader.Split('/').Last();

        var driftBody = new { prices = new Dictionary<string, decimal> { ["SPY"] = 500m } };
        var driftResponse = await _client.PostAsJsonAsync($"/api/portfolios/{id}/drift", driftBody);
        driftResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HistoryEndpoint_ReturnsOwnEvents()
    {
        var createBody = new
        {
            name = "History Owner Test",
            driftTolerancePct = 5.0m,
            allocations = new[] { new { ticker = "SPY", weight = 1.0 } }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/portfolios", createBody);
        var locationHeader = createResponse.Headers.Location!.ToString();
        var id = locationHeader.Split('/').Last();

        var rebalanceBody = new { prices = new Dictionary<string, decimal> { ["SPY"] = 500m } };
        var rebalanceResponse = await _client.PostAsJsonAsync($"/api/portfolios/{id}/rebalance", rebalanceBody);
        rebalanceResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var historyResponse = await _client.GetAsync($"/api/portfolios/{id}/rebalance");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        events.GetArrayLength().Should().Be(1);
        events[0].GetProperty("totalPortfolioValue").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task HistoryEndpoint_ReturnsEmptyForOtherUsersPortfolio()
    {
        var createBody = new
        {
            name = "History IDOR Test",
            driftTolerancePct = 5.0m,
            allocations = new[] { new { ticker = "SPY", weight = 1.0 } }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/portfolios", createBody);
        var locationHeader = createResponse.Headers.Location!.ToString();
        var id = locationHeader.Split('/').Last();

        var rebalanceBody = new { prices = new Dictionary<string, decimal> { ["SPY"] = 500m } };
        await _client.PostAsJsonAsync($"/api/portfolios/{id}/rebalance", rebalanceBody);

        using var otherUserClient = _factory.CreateClient();
        otherUserClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwt.Generate("a-different-user"));

        var historyResponse = await otherUserClient.GetAsync($"/api/portfolios/{id}/rebalance");
        historyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var events = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        events.GetArrayLength().Should().Be(0);
    }
}
