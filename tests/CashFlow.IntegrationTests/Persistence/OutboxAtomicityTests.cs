using System.Text.Json;
using CashFlow.Application.Outbox;
using CashFlow.Application.Transactions;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// A garantia central de ADR-004: o lançamento e o evento chegam ao banco na
/// mesma transação. Contra dublês isso é uma expectativa sobre uma chamada;
/// aqui é o comportamento do PostgreSQL.
/// </summary>
[Collection(nameof(CashFlowDatabaseCollection))]
[Trait("Category", "Integration")]
public class OutboxAtomicityTests : IAsyncLifetime
{
    private readonly CashFlowDatabaseFixture _fixture;

    public OutboxAtomicityTests(CashFlowDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisterTransaction_ShouldPersistTheTransactionAndItsEventTogether()
    {
        await using (var writeContext = _fixture.CreateContext())
        {
            var useCase = new RegisterTransactionUseCase(
                new TransactionRepository(writeContext),
                new OutboxRepository(writeContext),
                new UnitOfWork(writeContext));

            await useCase.Handle(
                new RegisterTransactionCommand(
                    Amount: 250.30m,
                    Type: "DEBIT",
                    OccurredAt: new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero),
                    Description: "Compra de insumos",
                    CorrelationId: Guid.NewGuid()),
                CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();

        var transaction = await readContext.Transactions.AsNoTracking().SingleAsync();
        var message = await readContext.OutboxMessages.AsNoTracking().SingleAsync();

        message.Type.Should().Be(TransactionRegisteredEvent.Type);
        message.ProcessedAt.Should().BeNull("a publicação é assíncrona e ainda não aconteceu");
        message.Attempts.Should().Be(0);

        // O payload gravado é o envelope que será publicado, sem retrabalho no
        // publisher — inclusive o `data.occurredAt`, que é o fato econômico, e
        // não o instante da emissão (api-contracts.md §5).
        var envelope = JsonSerializer.Deserialize<TransactionRegisteredEvent>(
            message.Payload, IntegrationEvents.SerializerOptions);

        envelope!.EventId.Should().Be(message.Id);
        envelope.Data.TransactionId.Should().Be(transaction.Id);
        envelope.Data.Amount.Should().Be(250.30m);
        envelope.Data.Type.Should().Be("DEBIT");
        envelope.Data.OccurredAt.Should().Be(transaction.OccurredAt);
    }

    [Fact]
    public async Task FailureToWriteTheOutbox_ShouldRollBackTheTransaction()
    {
        var duplicatedEventId = Guid.NewGuid();
        await PersistOutboxMessage(duplicatedEventId);

        await using var writeContext = _fixture.CreateContext();
        var transaction = Domain.Entities.Transaction.Create(
            Domain.ValueObjects.Money.Create(99.99m),
            Domain.ValueObjects.TransactionType.Credit,
            new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero),
            description: null);

        await new TransactionRepository(writeContext).AddAsync(transaction, CancellationToken.None);

        // Mesmo eventId de uma mensagem já gravada: a chave primária do outbox
        // recusa a inserção no meio do commit.
        await new OutboxRepository(writeContext).AddAsync(
            BuildMessage(duplicatedEventId), CancellationToken.None);

        var save = async () => await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);

        await save.Should().ThrowAsync<DbUpdateException>();

        // O que o teste existe para provar: o lançamento não sobreviveu sozinho.
        // Se ele tivesse sido gravado em uma transação própria, estaria aqui — e
        // o mundo nunca ficaria sabendo dele.
        await using var readContext = _fixture.CreateContext();
        var persisted = await readContext.Transactions.AsNoTracking().ToListAsync();

        persisted.Should().BeEmpty();
    }

    private static OutboxMessage BuildMessage(Guid eventId) =>
        OutboxMessage.Create(
            eventId,
            TransactionRegisteredEvent.Type,
            """{"eventId":"00000000-0000-0000-0000-000000000000"}""",
            new DateTimeOffset(2026, 6, 2, 8, 0, 0, TimeSpan.Zero));

    private async Task PersistOutboxMessage(Guid eventId)
    {
        await using var context = _fixture.CreateContext();

        await new OutboxRepository(context).AddAsync(BuildMessage(eventId), CancellationToken.None);
        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }
}
