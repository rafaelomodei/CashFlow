using System.Text;
using System.Text.Json;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using CashFlow.Application.Transactions;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CashFlow.IntegrationTests.Messaging;

/// <summary>
/// O ciclo completo do outbox contra banco e broker reais (ADR-004, RNF-001,
/// RNF-007).
/// </summary>
[Collection(nameof(MessagingCollection))]
[Trait("Category", "Integration")]
public class OutboxPublishingTests : IAsyncLifetime
{
    private readonly MessagingFixture _fixture;

    public OutboxPublishingTests(MessagingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.DrainQueueAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteredTransaction_ShouldReachTheQueueAfterTheNextCycle()
    {
        var transaction = await RegisterTransaction(1500.00m, "CREDIT");

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        var result = await RunPublishCycle(new RabbitMqEventPublisher(provider));

        result.Published.Should().Be(1);
        result.Failed.Should().Be(0);

        var delivered = await _fixture.DrainQueueAsync();
        var envelope = Deserialize(Encoding.UTF8.GetString(delivered.Single().Body.Span));

        envelope.Data.TransactionId.Should().Be(transaction.Id);
        envelope.Data.Amount.Should().Be(1500.00m);
        envelope.Data.Type.Should().Be("CREDIT");
        envelope.Data.OccurredAt.Should().Be(transaction.OccurredAt);

        await using var context = _fixture.CreateContext();
        var stored = await context.OutboxMessages.AsNoTracking().SingleAsync();
        stored.ProcessedAt.Should().NotBeNull("a mensagem só sai de pendente após o ack do broker");
    }

    [Fact]
    public async Task WithTheBrokerUnreachable_RegisteringShouldStillSucceedAndTheEventShouldStayPending()
    {
        // O caso que a arquitetura inteira existe para sustentar: o broker fora
        // do ar não pode impedir o registro do lançamento (RNF-001). O caso de
        // uso nem conhece o publisher — o teste confirma que a gravação acontece
        // e que o evento fica retido, não perdido.
        var transaction = await RegisterTransaction(90.50m, "DEBIT");

        await using var provider = _fixture.CreateConnectionProvider(MessagingFixture.UnreachableBrokerOptions());
        var result = await RunPublishCycle(new RabbitMqEventPublisher(provider));

        result.Published.Should().Be(0);
        result.Failed.Should().Be(1);

        await using var context = _fixture.CreateContext();

        (await context.Transactions.AsNoTracking().SingleAsync()).Id.Should().Be(transaction.Id);

        var stored = await context.OutboxMessages.AsNoTracking().SingleAsync();
        stored.ProcessedAt.Should().BeNull();
        stored.Attempts.Should().Be(1);
        stored.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenTheBrokerComesBack_TheSamePublisherShouldRecoverItsDeadConnection()
    {
        // Uma publicação bem-sucedida **antes** da queda é o que dá sentido ao
        // teste: sem ela o publisher nunca teria conexão, e o que seria exercitado
        // depois é uma primeira conexão, não uma reconexão.
        await RegisterTransaction(10.00m, "CREDIT");

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        var publisher = new RabbitMqEventPublisher(provider);

        (await RunPublishCycle(publisher)).Published.Should().Be(1);
        await _fixture.DrainQueueAsync();

        await RegisterTransaction(20.00m, "CREDIT");
        await RegisterTransaction(30.00m, "CREDIT");

        await _fixture.StopBrokerAsync();
        var duringOutage = await RunPublishCycle(publisher);
        duringOutage.Published.Should().Be(0);
        duringOutage.Failed.Should().Be(2);

        // O broker volta com a fila vazia — o container é recriado sem volume.
        // A topologia precisa ser redeclarada, ou as mensagens seriam publicadas
        // em um exchange sem fila ligada e sumiriam sem erro.
        await _fixture.StartBrokerAsync();
        var afterRecovery = await RunPublishCycle(publisher);

        afterRecovery.Published.Should().Be(2);
        afterRecovery.Failed.Should().Be(0);
        (await _fixture.DrainQueueAsync()).Should().HaveCount(2);

        await using var context = _fixture.CreateContext();
        var stored = await context.OutboxMessages.AsNoTracking()
            .Where(message => message.Attempts > 0).ToListAsync();

        stored.Should().HaveCount(2).And.OnlyContain(message => message.ProcessedAt != null);
    }

    private static TransactionRegisteredEvent Deserialize(string payload) =>
        JsonSerializer.Deserialize<TransactionRegisteredEvent>(payload, IntegrationEvents.SerializerOptions)!;

    private async Task<TransactionDto> RegisterTransaction(decimal amount, string type)
    {
        await using var context = _fixture.CreateContext();

        var useCase = new RegisterTransactionUseCase(
            new TransactionRepository(context),
            new OutboxRepository(context),
            new UnitOfWork(context));

        return await useCase.Handle(
            new RegisterTransactionCommand(
                amount, type, new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), null, Guid.NewGuid()),
            CancellationToken.None);
    }

    private async Task<OutboxPublishResult> RunPublishCycle(IEventPublisher publisher)
    {
        await using var context = _fixture.CreateContext();

        return await new PublishPendingOutboxMessagesUseCase(
                new OutboxRepository(context), publisher, new UnitOfWork(context))
            .Handle(batchSize: 100, CancellationToken.None);
    }
}
