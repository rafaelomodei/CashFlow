using CashFlow.Application.Outbox;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// Leitura e marcação de mensagens do outbox (ADR-004). A publicação em si é da
/// etapa 9; o que se verifica aqui é o estado que ela vai consumir.
/// </summary>
[Collection(nameof(CashFlowDatabaseCollection))]
[Trait("Category", "Integration")]
public class OutboxRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Emission = new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly CashFlowDatabaseFixture _fixture;

    public OutboxRepositoryTests(CashFlowDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetPendingAsync_ShouldReturnOnlyUnpublishedMessagesOldestFirst()
    {
        var oldest = Message(Emission);
        var newest = Message(Emission.AddMinutes(5));
        var alreadyPublished = Message(Emission.AddMinutes(1));
        alreadyPublished.MarkAsProcessed();

        await Persist(newest, alreadyPublished, oldest);

        await using var context = _fixture.CreateContext();
        var pending = await new OutboxRepository(context).GetPendingAsync(10, CancellationToken.None);

        // Da mais antiga para a mais nova: é a ordem em que os fatos
        // aconteceram, e é nela que o consumidor deve vê-los.
        pending.Select(message => message.Id).Should().Equal(oldest.Id, newest.Id);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectTheBatchSize()
    {
        await Persist(Message(Emission), Message(Emission.AddMinutes(1)), Message(Emission.AddMinutes(2)));

        await using var context = _fixture.CreateContext();
        var pending = await new OutboxRepository(context).GetPendingAsync(2, CancellationToken.None);

        pending.Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkAsProcessed_ShouldRemoveTheMessageFromThePendingSet()
    {
        var message = Message(Emission);
        await Persist(message);

        await using (var context = _fixture.CreateContext())
        {
            var repository = new OutboxRepository(context);
            var pending = await repository.GetPendingAsync(10, CancellationToken.None);
            pending.Single().MarkAsProcessed();

            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();
        var stillPending = await new OutboxRepository(readContext).GetPendingAsync(10, CancellationToken.None);

        stillPending.Should().BeEmpty();
        var stored = await readContext.OutboxMessages.AsNoTracking().SingleAsync();
        stored.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterFailure_ShouldKeepTheMessagePendingAndRecordTheAttempt()
    {
        var message = Message(Emission);
        await Persist(message);

        await using (var context = _fixture.CreateContext())
        {
            var repository = new OutboxRepository(context);
            var pending = await repository.GetPendingAsync(10, CancellationToken.None);
            pending.Single().RegisterFailure("broker unreachable");

            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        // Falhar em publicar não pode fazer o evento desaparecer (RNF-007).
        await using var readContext = _fixture.CreateContext();
        var stored = (await new OutboxRepository(readContext).GetPendingAsync(10, CancellationToken.None)).Single();

        stored.Attempts.Should().Be(1);
        stored.Error.Should().Be("broker unreachable");
        stored.ProcessedAt.Should().BeNull();
    }

    private static OutboxMessage Message(DateTimeOffset occurredAt) =>
        OutboxMessage.Create(
            Guid.CreateVersion7(),
            TransactionRegisteredEvent.Type,
            """{"eventType":"TransactionRegistered"}""",
            occurredAt);

    private async Task Persist(params OutboxMessage[] messages)
    {
        await using var context = _fixture.CreateContext();
        var repository = new OutboxRepository(context);

        foreach (var message in messages)
        {
            await repository.AddAsync(message, CancellationToken.None);
        }

        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }
}
