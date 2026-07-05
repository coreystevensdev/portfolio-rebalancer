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
