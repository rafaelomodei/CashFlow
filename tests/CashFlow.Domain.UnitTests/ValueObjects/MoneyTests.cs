using System.Globalization;
using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;

namespace CashFlow.Domain.UnitTests.ValueObjects;

/// <summary>
/// RN-001 e ADR-013: o valor de um lançamento é sempre positivo e tem no máximo
/// duas casas decimais.
/// </summary>
[Trait("Category", "Unit")]
public class MoneyTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(-9999.99)]
    public void Create_WithNegativeAmount_ShouldThrowDomainException(decimal amount)
    {
        var act = () => Money.Create(amount);

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must be greater than zero.");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrowDomainException()
    {
        var act = () => Money.Create(0m);

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must be greater than zero.");
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("10.555")]
    [InlineData("1500.0001")]
    public void Create_WithMoreThanTwoDecimalPlaces_ShouldThrowDomainException(string amount)
    {
        var act = () => Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture));

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must have at most two decimal places.");
    }

    [Fact]
    public void Create_WithAmountAboveTheSupportedRange_ShouldThrowDomainException()
    {
        var aboveRange = 10_000_000_000_000_000.00m;

        var act = () => Money.Create(aboveRange);

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must not exceed 9999999999999999.99.");
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("1500.00")]
    [InlineData("9999999999999999.99")]
    public void Create_WithAmountInsideTheSupportedRange_ShouldKeepTheValue(string amount)
    {
        var expected = decimal.Parse(amount, CultureInfo.InvariantCulture);

        var money = Money.Create(expected);

        money.Amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("10.5", "10.50")]
    [InlineData("1500", "1500.00")]
    [InlineData("0.1", "0.10")]
    public void Create_WithFewerThanTwoDecimalPlaces_ShouldNormalizeTheScale(string amount, string expected)
    {
        var money = Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture));

        money.Amount.ToString(CultureInfo.InvariantCulture).Should().Be(expected);
    }

    [Fact]
    public void Equals_WithTheSameAmount_ShouldBeEqualByValue()
    {
        var one = Money.Create(1500.00m);
        var other = Money.Create(1500.00m);

        one.Should().Be(other);
        one.GetHashCode().Should().Be(other.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentAmounts_ShouldNotBeEqual()
    {
        var one = Money.Create(1500.00m);
        var other = Money.Create(1500.01m);

        one.Should().NotBe(other);
    }
}
