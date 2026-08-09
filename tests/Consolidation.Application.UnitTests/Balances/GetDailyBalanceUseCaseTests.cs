using Consolidation.Application.Abstractions;
using Consolidation.Application.Balances;
using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Consolidation.Application.UnitTests.Balances;

/// <summary>
/// UC-02 (RF-004, RF-005, RF-006). Dia sem lançamentos tem saldo zero — ele não
/// deixa de existir (ADR-006).
/// </summary>
[Trait("Category", "Unit")]
public class GetDailyBalanceUseCaseTests
{
    private static readonly DateOnly Day = new(2026, 8, 8);

    private readonly IDailyBalanceRepository _balances = Substitute.For<IDailyBalanceRepository>();
    private readonly GetDailyBalanceUseCase _useCase;

    public GetDailyBalanceUseCaseTests()
    {
        _useCase = new GetDailyBalanceUseCase(_balances);
    }

    [Fact]
    public async Task Handle_WhenTheDayHasBeenConsolidated_ShouldReturnItsBalance()
    {
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));
        balance.Apply(TransactionType.Debit, Money.Create(700.00m));
        _balances.GetAsync(Day, Arg.Any<CancellationToken>()).Returns(balance);

        var result = await _useCase.Handle(Day, CancellationToken.None);

        result.Date.Should().Be(Day);
        result.TotalCredits.Should().Be(1500.00m);
        result.TotalDebits.Should().Be(700.00m, "o total de débitos é positivo, sem sinal");
        result.Balance.Should().Be(800.00m);
    }

    [Fact]
    public async Task Handle_ShouldExposeWhenTheDayWasLastConsolidated()
    {
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));
        _balances.GetAsync(Day, Arg.Any<CancellationToken>()).Returns(balance);

        var result = await _useCase.Handle(Day, CancellationToken.None);

        result.UpdatedAt.Should().Be(balance.UpdatedAt,
            "updatedAt é a evidência observável da defasagem da consolidação (ADR-006)");
    }

    [Fact]
    public async Task Handle_WhenTheDayHasNoTransactions_ShouldReturnAZeroedBalance()
    {
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);

        var result = await _useCase.Handle(Day, CancellationToken.None);

        result.Date.Should().Be(Day);
        result.TotalCredits.Should().Be(0.00m);
        result.TotalDebits.Should().Be(0.00m);
        result.Balance.Should().Be(0.00m);
    }

    [Fact]
    public async Task Handle_WhenTheDayHasNoTransactions_ShouldReportThatItWasNeverConsolidated()
    {
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);

        var result = await _useCase.Handle(Day, CancellationToken.None);

        result.UpdatedAt.Should().BeNull("ausência de consolidação é informação, não erro");
    }

    [Fact]
    public async Task Handle_WithAFutureDate_ShouldAnswerLikeAnyOtherDayWithoutTransactions()
    {
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);
        var future = new DateOnly(2030, 1, 1);

        var result = await _useCase.Handle(future, CancellationToken.None);

        result.Date.Should().Be(future);
        result.Balance.Should().Be(0.00m);
        result.UpdatedAt.Should().BeNull();
    }
}
