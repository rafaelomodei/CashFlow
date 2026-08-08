using System.Text.Json;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace CashFlow.Application.UnitTests.Transactions;

/// <summary>
/// UC-01 (RF-001, RF-002). O ponto central: lançamento e evento nascem na mesma
/// unidade de trabalho, e nada nesse caminho passa pelo broker (ADR-004, RNF-001).
/// </summary>
[Trait("Category", "Unit")]
public class RegisterTransactionUseCaseTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 8, 14, 30, 0, TimeSpan.Zero);

    private static readonly Guid CorrelationId =
        Guid.Parse("b1f2c3d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IOutboxRepository _outbox = Substitute.For<IOutboxRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RegisterTransactionUseCase _useCase;

    public RegisterTransactionUseCaseTests()
    {
        _useCase = new RegisterTransactionUseCase(_transactions, _outbox, _unitOfWork);
    }

    private static RegisterTransactionCommand ValidCommand(
        decimal amount = 1500.00m,
        string type = "CREDIT",
        DateTimeOffset? occurredAt = null,
        string? description = "Venda no balcão") =>
        new(amount, type, occurredAt ?? OccurredAt, description, CorrelationId);

    [Fact]
    public async Task Handle_WithAValidCommand_ShouldPersistTheTransaction()
    {
        await _useCase.Handle(ValidCommand(), CancellationToken.None);

        var persisted = CapturedTransaction();
        persisted.Amount.Amount.Should().Be(1500.00m);
        persisted.Type.Should().Be(TransactionType.Credit);
        persisted.OccurredAt.Should().Be(OccurredAt);
        persisted.Description.Should().Be("Venda no balcão");
    }

    [Fact]
    public async Task Handle_WithAValidCommand_ShouldReturnTheRegisteredTransaction()
    {
        var result = await _useCase.Handle(ValidCommand(), CancellationToken.None);

        var persisted = CapturedTransaction();
        result.Id.Should().Be(persisted.Id);
        result.Type.Should().Be("CREDIT");
        result.Amount.Should().Be(1500.00m);
        result.OccurredAt.Should().Be(OccurredAt);
        result.Description.Should().Be("Venda no balcão");
        result.CreatedAt.Should().Be(persisted.CreatedAt);
    }

    [Fact]
    public async Task Handle_ShouldWriteTheTransactionAndTheEventInTheSameUnitOfWork()
    {
        await _useCase.Handle(ValidCommand(), CancellationToken.None);

        Received.InOrder(() =>
        {
            _transactions.AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
            _outbox.AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldWriteAnOutboxMessageThatMatchesTheEventContract()
    {
        await _useCase.Handle(ValidCommand(), CancellationToken.None);

        var message = CapturedOutboxMessage();
        var transaction = CapturedTransaction();
        var payload = JsonDocument.Parse(message.Payload).RootElement;

        message.Type.Should().Be("TransactionRegistered");
        message.Id.Should().NotBeEmpty();
        payload.GetProperty("eventId").GetGuid().Should().Be(message.Id,
            "o eventId do envelope é a chave de idempotência gravada no outbox");
        payload.GetProperty("eventType").GetString().Should().Be("TransactionRegistered");
        payload.GetProperty("eventVersion").GetInt32().Should().Be(1);
        payload.GetProperty("correlationId").GetGuid().Should().Be(CorrelationId);

        var data = payload.GetProperty("data");
        data.GetProperty("transactionId").GetGuid().Should().Be(transaction.Id);
        data.GetProperty("type").GetString().Should().Be("CREDIT");
        data.GetProperty("amount").GetDecimal().Should().Be(1500.00m);
        data.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(OccurredAt);
    }

    [Fact]
    public async Task Handle_ShouldDistinguishTheEmissionInstantFromTheEconomicFact()
    {
        // Lançamento retroativo: os dois occurredAt ficam a meses de distância, e
        // consolidar pelo envelope colocaria tudo no dia da emissão (contrato §5.2).
        var backdated = new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero);

        await _useCase.Handle(ValidCommand(occurredAt: backdated), CancellationToken.None);

        var payload = JsonDocument.Parse(CapturedOutboxMessage().Payload).RootElement;
        payload.GetProperty("data").GetProperty("occurredAt").GetDateTimeOffset().Should().Be(backdated);
        payload.GetProperty("occurredAt").GetDateTimeOffset().Should().BeAfter(backdated);
    }

    [Fact]
    public async Task Handle_ShouldUseTheSameInstantForTheEnvelopeAndForTheOutboxRow()
    {
        await _useCase.Handle(ValidCommand(), CancellationToken.None);

        var message = CapturedOutboxMessage();
        var payload = JsonDocument.Parse(message.Payload).RootElement;

        payload.GetProperty("occurredAt").GetDateTimeOffset().Should().Be(message.OccurredAt);
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheOutboxMessagePending()
    {
        await _useCase.Handle(ValidCommand(), CancellationToken.None);

        var message = CapturedOutboxMessage();
        message.ProcessedAt.Should().BeNull("publicar é trabalho do publisher, não do registro");
        message.Attempts.Should().Be(0);
        message.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1500.005)]
    public async Task Handle_WithAnInvalidAmount_ShouldNotPersistAnything(decimal amount)
    {
        var act = () => _useCase.Handle(ValidCommand(amount: amount), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidAmountException>();
        await AssertNothingWasPersisted();
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("TRANSFER")]
    [InlineData("")]
    public async Task Handle_WithAnInvalidType_ShouldNotPersistAnything(string type)
    {
        var act = () => _useCase.Handle(ValidCommand(type: type), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidTransactionTypeException>();
        await AssertNothingWasPersisted();
    }

    [Fact]
    public void UseCase_ShouldNotDependOnTheEventPublisher()
    {
        // RNF-001: registrar um lançamento não pode depender do broker. A garantia
        // mais forte não é um teste de comportamento — é a dependência não existir.
        var dependencies = typeof(RegisterTransactionUseCase)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        dependencies.Should().NotContain(typeof(IEventPublisher));
    }

    private Transaction CapturedTransaction()
    {
        var calls = _transactions.ReceivedCalls().ToList();
        calls.Should().NotBeEmpty("o lançamento precisa ser persistido");

        return (Transaction)calls[0].GetArguments()[0]!;
    }

    private OutboxMessage CapturedOutboxMessage()
    {
        var calls = _outbox.ReceivedCalls().ToList();
        calls.Should().NotBeEmpty("o evento precisa ser gravado no outbox");

        return (OutboxMessage)calls[0].GetArguments()[0]!;
    }

    private async Task AssertNothingWasPersisted()
    {
        await _transactions.DidNotReceive().AddAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await _outbox.DidNotReceive().AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
