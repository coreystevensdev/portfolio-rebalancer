using FluentAssertions;
using PortfolioRebalancer.Api.Domain;
using Xunit;

namespace PortfolioRebalancer.Tests.Domain;

public class DriftCalculatorTests
{
    private static Portfolio BuildPortfolio(decimal driftTolerance = 5m)
    {
        return Portfolio.Create(
            "user-1",
            "Test Portfolio",
            driftTolerance,
            [("AAPL", 0.6m), ("MSFT", 0.4m)]
        );
    }

    [Fact]
    public void ReturnsZeroValuesWhenPortfolioHasNoHoldings()
    {
        var portfolio = BuildPortfolio();
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m, ["MSFT"] = 100m };

        var report = DriftCalculator.Calculate(portfolio, prices);

        // Named for the empty case because that is what it builds. With a total
        // of 0 the actual weights are 0 and the drifts are -60 and -40, so this
        // is the maximum-drift case, not the balanced one. The balanced case is
        // the test below.
        report.TotalPortfolioValue.Should().Be(0m);
        report.Positions.Should().HaveCount(2);
        report.Positions.All(p => p.MarketValue == 0).Should().BeTrue();
    }

    [Fact]
    public void ReturnsZeroDriftWhenHoldingsMatchTheTargetExactly()
    {
        var portfolio = BuildPortfolio();
        // 60 shares AAPL @ $100 = $6,000 (60% actual vs 60% target = 0 drift)
        // 40 shares MSFT @ $100 = $4,000 (40% actual vs 40% target = 0 drift)
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "AAPL", [(60m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "MSFT", [(40m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m, ["MSFT"] = 100m };

        var report = DriftCalculator.Calculate(portfolio, prices);

        report.TotalPortfolioValue.Should().Be(10_000m);
        report.Positions.Single(p => p.Ticker == "AAPL").DriftPct.Should().Be(0m);
        report.Positions.Single(p => p.Ticker == "MSFT").DriftPct.Should().Be(0m);
        report.Positions.All(p => !p.IsOutOfBand).Should().BeTrue();
    }

    [Fact]
    public void ThrowsWhenPriceIsMissing()
    {
        var portfolio = BuildPortfolio();
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m }; // MSFT missing

        var act = () => DriftCalculator.Calculate(portfolio, prices);
        act.Should().Throw<DomainException>().WithMessage("*MSFT*");
    }

    [Fact]
    public void ThrowsWhenPriceIsZero()
    {
        var portfolio = BuildPortfolio();
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m, ["MSFT"] = 0m };

        var act = () => DriftCalculator.Calculate(portfolio, prices);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DetectsOutOfBandWhenDriftExceedsTolerance()
    {
        var portfolio = BuildPortfolio(driftTolerance: 5m);
        // 70 shares AAPL @ $100 = $7,000 (70% actual vs 60% target = +10% drift)
        // 30 shares MSFT @ $100 = $3,000 (30% actual vs 40% target = -10% drift)
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "AAPL", [(70m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "MSFT", [(30m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m, ["MSFT"] = 100m };

        var report = DriftCalculator.Calculate(portfolio, prices);

        report.Positions.Single(p => p.Ticker == "AAPL").IsOutOfBand.Should().BeTrue();
        report.Positions.Single(p => p.Ticker == "MSFT").IsOutOfBand.Should().BeTrue();
    }

    [Fact]
    public void DoesNotFlagInBandPositions()
    {
        var portfolio = BuildPortfolio(driftTolerance: 5m);
        // 63 shares AAPL @ $100 = $6,300 (63% actual vs 60% target = +3% drift -- within 5%)
        // 37 shares MSFT @ $100 = $3,700 (37% actual vs 40% target = -3% drift -- within 5%)
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "AAPL", [(63m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        portfolio.Holdings.Add(Holding.Create(portfolio.Id, "MSFT", [(37m, 90m, DateOnly.FromDateTime(DateTime.Today))]));
        var prices = new Dictionary<string, decimal> { ["AAPL"] = 100m, ["MSFT"] = 100m };

        var report = DriftCalculator.Calculate(portfolio, prices);

        report.Positions.All(p => !p.IsOutOfBand).Should().BeTrue();
    }
}
