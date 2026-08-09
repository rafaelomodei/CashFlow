using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// Persistência de lançamentos contra PostgreSQL real (RF-001, ADR-005).
/// </summary>
[Collection(nameof(CashFlowDatabaseCollection))]
[Trait("Category", "Integration")]
public class TransactionRepositoryTests : IAsyncLifetime
{
    private readonly CashFlowDatabaseFixture _fixture;

    public TransactionRepositoryTests(CashFlowDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTheTransactionThatWasPersisted()
    {
        var transaction = Transaction.Create(
            Money.Create(1500.75m),
            TransactionType.Credit,
            new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.Zero),
            "Venda balcão");

        await using (var writeContext = _fixture.CreateContext())
        {
            var repository = new TransactionRepository(writeContext);
            await repository.AddAsync(transaction, CancellationToken.None);
            await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);
        }

        // Contexto novo: um lançamento ainda rastreado pelo contexto de escrita
        // voltaria da memória, e o teste passaria sem que nada tivesse sido lido
        // do banco.
        await using var readContext = _fixture.CreateContext();
        var found = await new TransactionRepository(readContext).GetByIdAsync(transaction.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(transaction.Id);
        found.Amount.Should().Be(transaction.Amount);
        found.Type.Should().Be(TransactionType.Credit);
        found.OccurredAt.Should().Be(transaction.OccurredAt);
        found.Description.Should().Be("Venda balcão");
        found.CreatedAt.Should().BeCloseTo(transaction.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullWhenTheTransactionDoesNotExist()
    {
        await using var context = _fixture.CreateContext();

        var found = await new TransactionRepository(context).GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        found.Should().BeNull();
    }
}
