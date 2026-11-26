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

    // Pins a documented limitation rather than a desired behaviour. Sells are
    // sequenced first, which is an ordering guarantee; it was also claimed as a
    // funding one in the README, the demo page and this engine's own doc comment,
    // and that was false. Only out-of-band positions trade, so an overweight that
    // is still inside the tolerance band is never sold and its excess never funds
    // anything. If someone later makes the plan cash-neutral, this test fails and
    // the three prose sites plus Known Limitations have to change with it.
    [Fact]
    public void PlanIsNotCashNeutral_BecauseInBandOverweightsAreNeverSold()
    {
        // The seeded demo's shape: one out-of-band overweight funding two out-of-band
        // underweights, while a fourth position is overweight but inside the band.
        // VNQ holds $1,000 of excess that is never sold, and that is exactly the
        // shortfall. Weights sum to 1.0 on both sides so the arithmetic is honest.
        var report = BuildReport(100_000m, [
            ("VTI",  0.40m, 0.50m,  25m, true),   // $50,000 held, $40,000 target -> sell $10,000
            ("BND",  0.30m, 0.24m, -20m, true),   // $24,000 held, $30,000 target -> buy  $6,000
            ("VXUS", 0.20m, 0.15m, -25m, true),   // $15,000 held, $20,000 target -> buy  $5,000
            ("VNQ",  0.10m, 0.11m,  10m, false),  // $11,000 held, $10,000 target -> untouched
        ]);
        var prices = new Dictionary<string, decimal>
        {
            ["VTI"] = 100m, ["BND"] = 50m, ["VXUS"] = 25m, ["VNQ"] = 20m,
        };

        var orders = RebalancingEngine.GenerateOrders(report, prices);

        orders.Should().OnlyContain(o => o.Ticker != "VNQ", "an in-band position is never traded");
        var sold = orders.Where(o => o.Action == OrderAction.Sell).Sum(o => o.EstimatedValue);
        var bought = orders.Where(o => o.Action == OrderAction.Buy).Sum(o => o.EstimatedValue);
        sold.Should().Be(10_000m);
        bought.Should().Be(11_000m);
        (bought - sold).Should().Be(1_000m, "VNQ's in-band excess is never sold, so the plan needs outside cash");
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
