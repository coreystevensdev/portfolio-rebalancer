using FluentAssertions;
using PortfolioRebalancer.Api.Domain;
using Xunit;

namespace PortfolioRebalancer.Tests.Domain;

public class PortfolioTests
{
    [Fact]
    public void CreateSucceeds_WithValidInputs()
    {
        var portfolio = Portfolio.Create("user-1", "My Portfolio", 5m, [("AAPL", 0.6m), ("MSFT", 0.4m)]);

        portfolio.UserId.Should().Be("user-1");
        portfolio.Name.Should().Be("My Portfolio");
        portfolio.DriftTolerancePct.Should().Be(5m);
        portfolio.TargetAllocations.Should().HaveCount(2);
        portfolio.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_NormalizesTickersToUpperCase()
    {
        var portfolio = Portfolio.Create("u", "P", 5m, [("aapl", 0.5m), ("msft", 0.5m)]);
        portfolio.TargetAllocations.Select(a => a.Ticker).Should().Equal("AAPL", "MSFT");
    }

    // Normalising to upper case is what makes this reachable: ("aapl", 0.5) and
    // ("AAPL", 0.5) both become AAPL and the pair still sums to 1.0, so without a
    // guard the portfolio stored two rows for one ticker. Every later /drift and
    // /rebalance then threw ArgumentException out of DriftCalculator's ToDictionary,
    // which is not a DomainException, so the caller got a 500 and no route could
    // repair the row.
    [Theory]
    [InlineData("AAPL", "AAPL")]
    [InlineData("aapl", "AAPL")]
    [InlineData("  AAPL  ", "aapl")]
    public void Create_RejectsDuplicateTickers_IncludingCaseAndWhitespaceVariants(string first, string second)
    {
        var act = () => Portfolio.Create("u", "P", 5m, [(first, 0.5m), (second, 0.5m)]);

        act.Should().Throw<DomainException>().WithMessage("*AAPL*more than once*");
    }

    [Fact]
    public void Create_StillAcceptsDistinctTickers()
    {
        var act = () => Portfolio.Create("u", "P", 5m, [("AAPL", 0.5m), ("MSFT", 0.5m)]);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ThrowsWhenUserIdIsEmpty(string userId)
    {
        var act = () => Portfolio.Create(userId, "P", 5m, [("AAPL", 1m)]);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ThrowsWhenAllocationsDoNotSumToOne()
    {
        var act = () => Portfolio.Create("u", "P", 5m, [("AAPL", 0.5m), ("MSFT", 0.3m)]);
        act.Should().Throw<DomainException>().WithMessage("*sum*");
    }

    [Fact]
    public void Create_ThrowsWhenNoAllocations()
    {
        var act = () => Portfolio.Create("u", "P", 5m, []);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(51)]
    public void Create_ThrowsWhenDriftToleranceOutOfRange(decimal tolerance)
    {
        var act = () => Portfolio.Create("u", "P", tolerance, [("AAPL", 1m)]);
        act.Should().Throw<DomainException>();
    }
}
