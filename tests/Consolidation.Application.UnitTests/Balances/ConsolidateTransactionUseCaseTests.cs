using Consolidation.Application.Abstractions;
using Consolidation.Application.Balances;
using Consolidation.Application.Idempotency;
using Consolidation.Domain.Entities;
using Consolidation.Domain.Exceptions;
using Consolidation.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Shared.Contracts;

namespace Consolidation.Application.UnitTests.Balances;

/// <summary>
/// UC-04 (RF-004, RNF-008). Consolidar é somar: reprocessar sem proteção não
/// falha, apenas produz um saldo errado que parece certo (ADR-007).
/// </summary>
[Trait("Category", "Unit")]
public class ConsolidateTransactionUseCaseTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 8, 14, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Day = new(2026, 8, 8);

    private readonly IDailyBalanceRepository _balances = Substitute.For<IDailyBalanceRepository>();
    private readonly IProcessedEventRepository _processedEvents = Substitute.For<IProcessedEventRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ConsolidateTransactionUseCase _useCase;

    public ConsolidateTransactionUseCaseTests()
    {
        _useCase = new ConsolidateTransactionUseCase(_balances, _processedEvents, _unitOfWork);
    }

    private static TransactionRegisteredEvent Event(
        string type = "CREDIT",
        decimal amount = 1500.00m,
        DateTimeOffset? occurredAt = null,
        Guid? eventId = null) =>
        new()
        {
            EventId = eventId ?? Guid.CreateVersion7(),
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.CreateVersion7(),
            Data = new TransactionRegisteredData
            {
                TransactionId = Guid.CreateVersion7(),
                Type = type,
                Amount = amount,
                OccurredAt = occurredAt ?? OccurredAt,
            },
        };

    private void DayHas(DailyBalance balance) =>
        _balances.GetAsync(balance.Date, Arg.Any<CancellationToken>()).Returns(balance);

    private void EventAlreadyProcessed(Guid eventId) =>
        _processedEvents.HasProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);

    private DailyBalance CapturedNewBalance()
    {
        var call = _balances.ReceivedCalls()
            .Single(received => received.GetMethodInfo().Name == nameof(IDailyBalanceRepository.AddAsync));

        return (DailyBalance)call.GetArguments()[0]!;
    }

    [Fact]
    public async Task Handle_WithACredit_ShouldIncreaseTheBalanceOfTheDay()
    {
        var balance = DailyBalance.Empty(Day);
        DayHas(balance);

        await _useCase.Handle(Event(type: "CREDIT", amount: 1500.00m), CancellationToken.None);

        balance.TotalCredits.Should().Be(Money.Create(1500.00m));
        balance.Balance.Should().Be(Money.Create(1500.00m));
    }

    [Fact]
    public async Task Handle_WithADebit_ShouldReduceTheBalanceOfTheDay()
    {
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));
        DayHas(balance);

        await _useCase.Handle(Event(type: "DEBIT", amount: 700.00m), CancellationToken.None);

        balance.TotalDebits.Should().Be(Money.Create(700.00m));
        balance.Balance.Should().Be(Money.Create(800.00m));
    }

    [Fact]
    public async Task Handle_WhenTheDayHasNoBalanceYet_ShouldCreateIt()
    {
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);

        await _useCase.Handle(Event(type: "CREDIT", amount: 1500.00m), CancellationToken.None);

        var created = CapturedNewBalance();
        created.Date.Should().Be(Day);
        created.TotalCredits.Should().Be(Money.Create(1500.00m));
    }

    [Fact]
    public async Task Handle_WhenTheDayAlreadyHasABalance_ShouldNotCreateAnother()
    {
        DayHas(DailyBalance.Empty(Day));

        await _useCase.Handle(Event(), CancellationToken.None);

        await _balances.DidNotReceive().AddAsync(Arg.Any<DailyBalance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldConsolidateOnTheDayOfTheEconomicFact()
    {
        // 22h em Brasília pertence ao dia seguinte em UTC (RN-004, P-04). Usar o
        // instante da emissão colocaria todo lançamento retroativo no dia errado.
        var inBrasilia = new DateTimeOffset(2026, 8, 8, 22, 0, 0, TimeSpan.FromHours(-3));
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);

        await _useCase.Handle(Event(occurredAt: inBrasilia), CancellationToken.None);

        CapturedNewBalance().Date.Should().Be(new DateOnly(2026, 8, 9));
    }

    [Fact]
    public async Task Handle_ShouldConsolidateABackdatedTransactionOnItsOwnDay()
    {
        var backdated = new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero);
        _balances.GetAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((DailyBalance?)null);

        await _useCase.Handle(Event(occurredAt: backdated), CancellationToken.None);

        CapturedNewBalance().Date.Should().Be(new DateOnly(2026, 5, 2));
    }

    [Fact]
    public async Task Handle_ShouldRecordTheEventAsProcessedInTheSameUnitOfWork()
    {
        var integrationEvent = Event();
        DayHas(DailyBalance.Empty(Day));

        await _useCase.Handle(integrationEvent, CancellationToken.None);

        Received.InOrder(() =>
        {
            _processedEvents.AddAsync(
                Arg.Is<ProcessedEvent>(processed => processed.EventId == integrationEvent.EventId),
                Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAnEventAlreadyProcessed_ShouldNotChangeTheBalanceASecondTime()
    {
        var integrationEvent = Event(amount: 1500.00m);
        var balance = DailyBalance.Empty(Day);
        balance.Apply(TransactionType.Credit, Money.Create(1500.00m));
        DayHas(balance);
        EventAlreadyProcessed(integrationEvent.EventId);

        await _useCase.Handle(integrationEvent, CancellationToken.None);

        balance.TotalCredits.Should().Be(Money.Create(1500.00m), "a reentrega do mesmo evento é descartada");
        balance.Balance.Should().Be(Money.Create(1500.00m));
    }

    [Fact]
    public async Task Handle_WithAnEventAlreadyProcessed_ShouldNotWriteAnything()
    {
        var integrationEvent = Event();
        EventAlreadyProcessed(integrationEvent.EventId);

        await _useCase.Handle(integrationEvent, CancellationToken.None);

        await _processedEvents.DidNotReceive().AddAsync(Arg.Any<ProcessedEvent>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithTheSameEventTwice_ShouldApplyItOnlyOnce()
    {
        var integrationEvent = Event(amount: 1500.00m);
        var balance = DailyBalance.Empty(Day);
        DayHas(balance);

        await _useCase.Handle(integrationEvent, CancellationToken.None);
        EventAlreadyProcessed(integrationEvent.EventId);
        await _useCase.Handle(integrationEvent, CancellationToken.None);

        balance.TotalCredits.Should().Be(Money.Create(1500.00m));
    }

    [Fact]
    public async Task Handle_WithADifferentEventForTheSameTransaction_ShouldBeApplied()
    {
        // eventId identifica a mensagem, não o lançamento: dois eventos distintos
        // sobre o mesmo lançamento são dois fatos, e ambos contam (contrato §5.3).
        var balance = DailyBalance.Empty(Day);
        DayHas(balance);

        await _useCase.Handle(Event(amount: 1500.00m), CancellationToken.None);
        await _useCase.Handle(Event(amount: 1500.00m), CancellationToken.None);

        balance.TotalCredits.Should().Be(Money.Create(3000.00m));
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("TRANSFER")]
    public async Task Handle_WithATypeOutsideTheContract_ShouldBeRejected(string type)
    {
        DayHas(DailyBalance.Empty(Day));

        var act = () => _useCase.Handle(Event(type: type), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTransactionTypeException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1500.00)]
    public async Task Handle_WithAnAmountOutsideTheContract_ShouldBeRejected(decimal amount)
    {
        DayHas(DailyBalance.Empty(Day));

        var act = () => _useCase.Handle(Event(amount: amount), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidAmountException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
