using System.Globalization;
using Consolidation.Domain.Exceptions;
using Consolidation.Domain.ValueObjects;
using FluentAssertions;

namespace Consolidation.Domain.UnitTests.ValueObjects;

/// <summary>
/// Neste contexto o dinheiro é uma **quantidade**: totais e saldo. O sinal é
/// resultado legítimo — um dia pode fechar negativo (RF-004). A regra de valor
/// positivo pertence ao lançamento, no outro contexto (RN-001).
/// </summary>
[Trait("Category", "Unit")]
public class MoneyTests
{
    [Theory]
    [InlineData("0.001")]
    [InlineData("10.555")]
    [InlineData("-0.001")]
    public void Create_WithMoreThanTwoDecimalPlaces_ShouldThrowDomainException(string amount)
    {
        var act = () => Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture));

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must have at most two decimal places.");
    }

    [Theory]
    [InlineData("10000000000000000.00")]
    [InlineData("-10000000000000000.00")]
    public void Create_WithAmountOutsideTheSupportedRange_ShouldThrowDomainException(string amount)
    {
        var act = () => Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture));

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must be between -9999999999999999.99 and 9999999999999999.99.");
    }

    [Theory]
    [InlineData("1500.00")]
    [InlineData("0.00")]
    [InlineData("-700.00")]
    public void Create_WithAmountInsideTheSupportedRange_ShouldKeepTheValue(string amount)
    {
        var expected = decimal.Parse(amount, CultureInfo.InvariantCulture);

        var money = Money.Create(expected);

        money.Amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("10.5", "10.50")]
    [InlineData("1500", "1500.00")]
    [InlineData("-700", "-700.00")]
    public void Create_WithFewerThanTwoDecimalPlaces_ShouldNormalizeTheScale(string amount, string expected)
    {
        var money = Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture));

        money.Amount.ToString(CultureInfo.InvariantCulture).Should().Be(expected);
    }

    [Fact]
    public void Zero_ShouldBeTheNeutralAmount()
    {
        Money.Zero.Amount.Should().Be(0m);
        Money.Zero.Amount.ToString(CultureInfo.InvariantCulture).Should().Be("0.00");
    }

    [Fact]
    public void Add_ShouldReturnTheSum()
    {
        var result = Money.Create(1500.00m).Add(Money.Create(700.00m));

        result.Should().Be(Money.Create(2200.00m));
    }

    [Fact]
    public void Add_WithCents_ShouldNotAccumulateRoundingError()
    {
        var result = Enumerable.Range(0, 100)
            .Aggregate(Money.Zero, (total, _) => total.Add(Money.Create(0.01m)));

        result.Should().Be(Money.Create(1.00m));
    }

    [Fact]
    public void Subtract_ShouldReturnTheDifference()
    {
        var result = Money.Create(1500.00m).Subtract(Money.Create(700.00m));

        result.Should().Be(Money.Create(800.00m));
    }

    [Fact]
    public void Subtract_WhenTheResultIsBelowZero_ShouldReturnANegativeAmount()
    {
        var result = Money.Create(700.00m).Subtract(Money.Create(1500.00m));

        result.Should().Be(Money.Create(-800.00m));
    }

    [Fact]
    public void Equals_WithTheSameAmount_ShouldBeEqualByValue()
    {
        var one = Money.Create(1500.00m);
        var other = Money.Create(1500.00m);

        one.Should().Be(other);
        one.GetHashCode().Should().Be(other.GetHashCode());
    }
}
