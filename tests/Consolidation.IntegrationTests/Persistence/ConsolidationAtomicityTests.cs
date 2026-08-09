using Consolidation.Application.Idempotency;
using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using Consolidation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.IntegrationTests.Persistence;

/// <summary>
/// O ponto 2 da ADR-007: efeito no saldo e marcação do evento entram na mesma
/// transação. Se elas fossem separadas, um crash entre as duas deixaria o saldo
/// alterado sem o evento constar como processado — e a próxima reentrega somaria
/// de novo.
/// </summary>
[Collection(nameof(ConsolidationDatabaseCollection))]
[Trait("Category", "Integration")]
public class ConsolidationAtomicityTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 5, 20);

    private readonly ConsolidationDatabaseFixture _fixture;

    public ConsolidationAtomicityTests(ConsolidationDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task FailureToRecordTheEvent_ShouldRollBackTheEffectOnTheBalance()
    {
        var duplicatedEventId = Guid.NewGuid();
        await using (var setup = _fixture.CreateContext())
        {
            await new ProcessedEventRepository(setup).AddAsync(
                ProcessedEvent.Now(duplicatedEventId), CancellationToken.None);
            await new UnitOfWork(setup).SaveChangesAsync(CancellationToken.None);
        }

        await using (var writeContext = _fixture.CreateContext())
        {
            var balance = DailyBalance.Empty(Day);
            balance.Apply(TransactionType.Credit, Money.Create(500.00m));
            await new DailyBalanceRepository(writeContext).AddAsync(balance, CancellationToken.None);

            // Mesmo eventId já gravado: a chave primária recusa a inserção no meio
            // do commit, e o saldo não pode sobreviver a ela.
            await new ProcessedEventRepository(writeContext).AddAsync(
                ProcessedEvent.Now(duplicatedEventId), CancellationToken.None);

            var save = async () => await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);

            await save.Should().ThrowAsync<DbUpdateException>();
        }

        await using var readContext = _fixture.CreateContext();
        var persisted = await readContext.DailyBalances.AsNoTracking().ToListAsync();

        persisted.Should().BeEmpty("um saldo alterado sem o evento marcado seria somado de novo na reentrega");
    }

    [Fact]
    public async Task AppliedBalanceAndProcessedEvent_ShouldBeVisibleTogetherAfterTheCommit()
    {
        var eventId = Guid.NewGuid();

        await using (var writeContext = _fixture.CreateContext())
        {
            var balance = DailyBalance.Empty(Day);
            balance.Apply(TransactionType.Debit, Money.Create(120.45m));

            await new DailyBalanceRepository(writeContext).AddAsync(balance, CancellationToken.None);
            await new ProcessedEventRepository(writeContext).AddAsync(
                ProcessedEvent.Now(eventId), CancellationToken.None);
            await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();

        var stored = await new DailyBalanceRepository(readContext).GetAsync(Day, CancellationToken.None);
        var processed = await new ProcessedEventRepository(readContext)
            .HasProcessedAsync(eventId, CancellationToken.None);

        stored!.Balance.Amount.Should().Be(-120.45m);
        processed.Should().BeTrue();
    }
}
