using FluentAssertions;
using PortfolioRebalancer.Api.Domain;
using Xunit;

namespace PortfolioRebalancer.Tests.Domain;

public class RebalancingEngineTests
{
    private static DriftReport BuildReport(
        decimal totalValue,
        IReadOnlyList<(string ticker, decimal targetWeight, decimal actualWeight, decimal driftPct, bool outOfBand)> positions)
    {
        return new DriftReport(
            totalValue,
            positions.Select(p => new DriftPosition(
                p.ticker, p.targetWeight, p.actualWeight, p.driftPct, p.outOfBand,
                Shares: 0m,
                MarketValue: p.actualWeight * totalValue
            )).ToList()
        );
    }

    [Fact]
    public void ReturnsEmptyWhenNothingIsOutOfBand()
    {
        var report = BuildReport(100_000m, [
            ("AAPL", 0.6m, 0.6m, 0m, false),
            ("MSFT", 0.4m, 0.4m, 0m, false),
        ]);
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 150m, ["MSFT"] = 300m };

        var orders = RebalancingEngine.GenerateOrders(report, prices);
        orders.Should().BeEmpty();
    }

    [Fact]
    public void GeneratesSellBeforeBuy()
    {
        // AAPL is 10% over-weight (sell), MSFT is 10% under-weight (buy).
        var report = BuildReport(100_000m, [
            ("AAPL", 0.6m, 0.7m, 10m, true),
            ("MSFT", 0.4m, 0.3m, -10m, true),
        ]);
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 150m, ["MSFT"] = 300m };

        var orders = RebalancingEngine.GenerateOrders(report, prices);
        orders.Should().HaveCount(2);
        orders[0].Action.Should().Be(OrderAction.Sell);
        orders[1].Action.Should().Be(OrderAction.Buy);
    }

    [Fact]
    public void SellOrderAmountMatchesDeltaDollars()
    {
        // AAPL at 70k vs target 60k: sell 10k worth.
        var report = BuildReport(100_000m, [
            ("AAPL", 0.6m, 0.7m, 10m, true),
            ("MSFT", 0.4m, 0.4m, 0m, false),
        ]);
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 200m, ["MSFT"] = 300m };

        var orders = RebalancingEngine.GenerateOrders(report, prices);
        orders.Should().HaveCount(1);
        var sell = orders[0];
        sell.Action.Should().Be(OrderAction.Sell);
        sell.Ticker.Should().Be("AAPL");
        // delta = 60k - 70k = -10k; shares = 10_000 / 200 = 50
        sell.Shares.Should().BeApproximately(50m, 0.001m);
        sell.EstimatedValue.Should().Be(10_000m);
    }

    [Fact]
    public void BuyOrderAmountMatchesDeltaDollars()
    {
        // MSFT at 30k vs target 40k: buy 10k worth.
        var report = BuildReport(100_000m, [
            ("AAPL", 0.6m, 0.6m, 0m, false),
            ("MSFT", 0.4m, 0.3m, -10m, true),
        ]);
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 200m, ["MSFT"] = 250m };

        var orders = RebalancingEngine.GenerateOrders(report, prices);
        orders.Should().HaveCount(1);
        var buy = orders[0];
        buy.Action.Should().Be(OrderAction.Buy);
        buy.Ticker.Should().Be("MSFT");
        buy.Shares.Should().BeApproximately(40m, 0.001m);
        buy.EstimatedValue.Should().Be(10_000m);
    }

    [Fact]
    public void ThrowsWhenPriceMissingForOutOfBandPosition()
    {
        var report = BuildReport(100_000m, [
            ("AAPL", 0.6m, 0.7m, 10m, true),
        ]);
        var prices = new Dictionary<string, decimal>(); // AAPL missing

        var act = () => RebalancingEngine.GenerateOrders(report, prices);
        act.Should().Throw<DomainException>().WithMessage("*AAPL*");
    }
}
