using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using Consolidation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.IntegrationTests.Persistence;

/// <summary>
/// Persistência do saldo diário contra PostgreSQL real (RF-004, ADR-005).
/// </summary>
[Collection(nameof(ConsolidationDatabaseCollection))]
[Trait("Category", "Integration")]
public class DailyBalanceRepositoryTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 4, 10);

    private readonly ConsolidationDatabaseFixture _fixture;

    public DailyBalanceRepositoryTests(ConsolidationDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAsync_ShouldReturnTheBalanceThatWasPersisted()
    {
        await Persist(Day, credits: [1500.75m, 20.10m], debits: [700.30m]);

        await using var readContext = _fixture.CreateContext();
        var found = await new DailyBalanceRepository(readContext).GetAsync(Day, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Date.Should().Be(Day);
        found.TotalCredits.Amount.Should().Be(1520.85m);
        found.TotalDebits.Amount.Should().Be(700.30m);
        found.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullForADayThatWasNeverConsolidated()
    {
        await using var context = _fixture.CreateContext();

        var found = await new DailyBalanceRepository(context).GetAsync(Day, CancellationToken.None);

        // Nulo, e não DailyBalance.Empty: traduzir ausência em saldo zero é
        // decisão do caso de uso (ADR-006), não do repositório.
        found.Should().BeNull();
    }

    [Fact]
    public async Task Balance_ShouldBeDerivedFromTheTotalsAndNotStored()
    {
        await Persist(Day, credits: [100.00m], debits: [250.50m]);

        await using var readContext = _fixture.CreateContext();
        var found = await new DailyBalanceRepository(readContext).GetAsync(Day, CancellationToken.None);

        // Um dia pode fechar negativo, e o valor vem dos dois totais — não de uma
        // terceira coluna que poderia divergir deles.
        found!.Balance.Amount.Should().Be(-150.50m);

        var columns = await readContext.Database
            .SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'daily_balances'")
            .ToListAsync();

        columns.Should().NotContain("balance");
    }

    [Fact]
    public async Task Totals_ShouldSurviveTheRoundTripWithoutLosingPrecision()
    {
        // Cem centavos somam exatamente um real em decimal — o que numeric(18,2)
        // preserva e ponto flutuante não (ADR-013).
        await Persist(Day, credits: [.. Enumerable.Repeat(0.01m, 100)], debits: []);

        await using var readContext = _fixture.CreateContext();
        var found = await new DailyBalanceRepository(readContext).GetAsync(Day, CancellationToken.None);

        found!.TotalCredits.Amount.Should().Be(1.00m);
    }

    [Fact]
    public async Task Date_ShouldBeThePrimaryKey()
    {
        await Persist(Day, credits: [10.00m], debits: []);

        await using var context = _fixture.CreateContext();
        var duplicate = DailyBalance.Empty(Day);
        duplicate.Apply(TransactionType.Credit, Money.Create(10.00m));
        await new DailyBalanceRepository(context).AddAsync(duplicate, CancellationToken.None);

        var save = async () => await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        // Um dia tem um saldo, não vários: sem a chave primária, o upsert da
        // etapa 10 acumularia linhas em vez de atualizar a existente.
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task DaysAreIndependent_AndDoNotLeakIntoEachOther()
    {
        await Persist(Day, credits: [100.00m], debits: []);
        await Persist(Day.AddDays(1), credits: [], debits: [40.00m]);

        await using var readContext = _fixture.CreateContext();
        var repository = new DailyBalanceRepository(readContext);

        (await repository.GetAsync(Day, CancellationToken.None))!.Balance.Amount.Should().Be(100.00m);
        (await repository.GetAsync(Day.AddDays(1), CancellationToken.None))!.Balance.Amount.Should().Be(-40.00m);
    }

    private async Task Persist(DateOnly date, decimal[] credits, decimal[] debits)
    {
        var balance = DailyBalance.Empty(date);
        foreach (var credit in credits)
        {
            balance.Apply(TransactionType.Credit, Money.Create(credit));
        }

        foreach (var debit in debits)
        {
            balance.Apply(TransactionType.Debit, Money.Create(debit));
        }

        await using var context = _fixture.CreateContext();
        await new DailyBalanceRepository(context).AddAsync(balance, CancellationToken.None);
        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);
    }
}
