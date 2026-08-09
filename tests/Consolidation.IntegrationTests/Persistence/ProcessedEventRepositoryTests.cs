using Consolidation.Application.Idempotency;
using Consolidation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.IntegrationTests.Persistence;

/// <summary>
/// O registro de eventos processados (ADR-007). O que estes testes verificam não
/// é a consulta prévia — que é apenas o caminho barato —, e sim a chave primária,
/// que é a garantia de fato.
/// </summary>
[Collection(nameof(ConsolidationDatabaseCollection))]
[Trait("Category", "Integration")]
public class ProcessedEventRepositoryTests : IAsyncLifetime
{
    private readonly ConsolidationDatabaseFixture _fixture;

    public ProcessedEventRepositoryTests(ConsolidationDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HasProcessedAsync_ShouldBeFalseBeforeAndTrueAfterTheEventIsRecorded()
    {
        var eventId = Guid.NewGuid();

        await using (var context = _fixture.CreateContext())
        {
            var repository = new ProcessedEventRepository(context);

            (await repository.HasProcessedAsync(eventId, CancellationToken.None)).Should().BeFalse();

            await repository.AddAsync(ProcessedEvent.Now(eventId), CancellationToken.None);
            await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();

        (await new ProcessedEventRepository(readContext).HasProcessedAsync(eventId, CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task HasProcessedAsync_ShouldNotConfuseOneEventWithAnother()
    {
        var recorded = Guid.NewGuid();
        await Persist(recorded);

        await using var context = _fixture.CreateContext();

        (await new ProcessedEventRepository(context).HasProcessedAsync(Guid.NewGuid(), CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task EventId_ShouldBeThePrimaryKey()
    {
        var eventId = Guid.NewGuid();
        await Persist(eventId);

        await using var context = _fixture.CreateContext();
        await new ProcessedEventRepository(context).AddAsync(ProcessedEvent.Now(eventId), CancellationToken.None);

        var save = async () => await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        // É esta violação que transforma reprocessamento em erro detectável em vez
        // de soma duplicada. Dois consumidores concorrentes podem passar os dois
        // pela consulta prévia; quem decide é o commit (ADR-007).
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    private async Task Persist(Guid eventId)
    {
        await using var context = _fixture.CreateContext();

        await new ProcessedEventRepository(context).AddAsync(ProcessedEvent.Now(eventId), CancellationToken.None);
        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }
}
