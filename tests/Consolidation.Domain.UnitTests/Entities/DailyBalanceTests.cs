using Consolidation.Domain.Entities;
using Consolidation.Domain.Exceptions;
using Consolidation.Domain.ValueObjects;
using FluentAssertions;

namespace Consolidation.Domain.UnitTests.Entities;

/// <summary>
/// RF-004: o saldo do dia é a soma dos créditos menos a soma dos débitos.
/// </summary>
[Trait("Category", "Unit")]
public class DailyBalanceTests
{
    private static readonly DateOnly Day = new(2026, 8, 8);

    [Fact]
    public void Empty_ShouldStartWithZeroedTotalsAndNoUpdate()
    {
        var balance = DailyBalance.Empty(Day);

        balance.Date.Should().Be(Day);
        balance.TotalCredits.Should().Be(Money.Zero);
        balance.TotalDebits.Should().Be(Money.Zero);
        balance.Balance.Should().Be(Money.Zero);
        balance.UpdatedAt.Should().BeNull("um dia sem lançamentos nunca foi consolidado");
    }

    [Fact]
    public void Apply_WithACredit_ShouldIncreaseTheBalance()
    {
        var balance = DailyBalance.Empty(Day);

        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));

        balance.TotalCredits.Should().Be(Money.Create(1500.00m));
        balance.TotalDebits.Should().Be(Money.Zero);
        balance.Balance.Should().Be(Money.Create(1500.00m));
    }

    [Fact]
    public void Apply_WithADebit_ShouldDecreaseTheBalance()
    {
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));

        balance.Apply(TransactionType.Debit, Money.Create(700.00m));

        balance.TotalCredits.Should().Be(Money.Create(1500.00m));
        balance.TotalDebits.Should().Be(Money.Create(700.00m),
            "o total de débitos é positivo, sem sinal");
        balance.Balance.Should().Be(Money.Create(800.00m));
    }

    [Fact]
    public void Balance_ShouldBeTotalCreditsMinusTotalDebits()
    {
        var balance = DailyBalance.Empty(Day);

        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));
        balance.Apply(TransactionType.Credit, Money.Create(250.50m));
        balance.Apply(TransactionType.Debit, Money.Create(700.00m));

        balance.TotalCredits.Should().Be(Money.Create(1750.50m));
        balance.TotalDebits.Should().Be(Money.Create(700.00m));
        balance.Balance.Should().Be(Money.Create(1050.50m));
    }

    [Fact]
    public void Balance_WhenDebitsExceedCredits_ShouldBeNegative()
    {
        var balance = DailyBalance.Empty(Day);

        balance.Apply(TransactionType.Credit, Money.Create(700.00m));
        balance.Apply(TransactionType.Debit, Money.Create(1500.00m));

        balance.Balance.Should().Be(Money.Create(-800.00m));
    }

    [Fact]
    public void Apply_ShouldRecordWhenTheDayWasLastConsolidated()
    {
        var balance = DailyBalance.Empty(Day);
        var before = DateTimeOffset.UtcNow;

        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));

        balance.UpdatedAt.Should().NotBeNull();
        balance.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Apply_WithAnAmountThatIsNotPositive_ShouldThrowDomainException(decimal amount)
    {
        var balance = DailyBalance.Empty(Day);

        var act = () => balance.Apply(TransactionType.Credit, Money.Create(amount));

        act.Should().Throw<InvalidAmountException>()
            .WithMessage("Amount must be greater than zero.");
    }

    [Fact]
    public void Apply_WithUndefinedType_ShouldThrowDomainException()
    {
        var balance = DailyBalance.Empty(Day);

        var act = () => balance.Apply((TransactionType)99, Money.Create(1500.00m));

        act.Should().Throw<InvalidTransactionTypeException>()
            .WithMessage("Type must be either CREDIT or DEBIT.");
    }

    [Fact]
    public void Apply_WithAnInvalidAmount_ShouldLeaveTheBalanceUntouched()
    {
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));

        var act = () => balance.Apply((TransactionType)99, Money.Create(700.00m));

        act.Should().Throw<InvalidTransactionTypeException>();
        balance.TotalCredits.Should().Be(Money.Create(1500.00m));
        balance.TotalDebits.Should().Be(Money.Zero);
    }

    [Fact]
    public void DayOf_ShouldUseTheUtcDateOfTheOccurrence()
    {
        // 22h em Brasília (UTC−3) é 01h do dia seguinte em UTC: a consolidação
        // segue a data civil em UTC (RN-004, premissa P-04, ADR-013).
        var inBrasilia = new DateTimeOffset(2026, 8, 8, 22, 0, 0, TimeSpan.FromHours(-3));

        var day = DailyBalance.DayOf(inBrasilia);

        day.Should().Be(new DateOnly(2026, 8, 9));
    }

    [Fact]
    public void DayOf_WhenTheOccurrenceIsAlreadyUtc_ShouldUseItsOwnDate()
    {
        var inUtc = new DateTimeOffset(2026, 8, 8, 14, 30, 0, TimeSpan.Zero);

        var day = DailyBalance.DayOf(inUtc);

        day.Should().Be(new DateOnly(2026, 8, 8));
    }
}
