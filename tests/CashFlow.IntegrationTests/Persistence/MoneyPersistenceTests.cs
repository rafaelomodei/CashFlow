using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// Exatidão decimal em <c>numeric(18,2)</c> (ADR-013, RN-001). É o teste que só
/// tem sentido contra o banco real: em memória, qualquer tipo guarda o valor de
/// volta — o que está em jogo aqui é a coluna.
/// </summary>
[Collection(nameof(CashFlowDatabaseCollection))]
[Trait("Category", "Integration")]
public class MoneyPersistenceTests : IAsyncLifetime
{
    private readonly CashFlowDatabaseFixture _fixture;

    public MoneyPersistenceTests(CashFlowDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("0.01")]
    [InlineData("10.50")]
    [InlineData("1500.75")]
    [InlineData("0.10")]
    // Limite superior de numeric(18,2): 16 dígitos inteiros e 2 decimais. O
    // domínio recusa acima disso justamente para que o estouro não vire erro de
    // banco.
    [InlineData("9999999999999999.99")]
    public async Task Amount_ShouldSurviveTheRoundTripUnchanged(string rawAmount)
    {
        var amount = decimal.Parse(rawAmount, System.Globalization.CultureInfo.InvariantCulture);
        var transaction = Transaction.Create(
            Money.Create(amount),
            TransactionType.Debit,
            new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero),
            description: null);

        await using (var writeContext = _fixture.CreateContext())
        {
            await new TransactionRepository(writeContext).AddAsync(transaction, CancellationToken.None);
            await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();
        var found = await new TransactionRepository(readContext).GetByIdAsync(transaction.Id, CancellationToken.None);

        found!.Amount.Amount.Should().Be(amount);
    }

    [Fact]
    public async Task Amounts_ShouldSumInTheDatabaseWithoutRoundingError()
    {
        // Cem centavos somam exatamente um real em decimal, e não em ponto
        // flutuante — onde 100 × 0.01 é 1.0000000000000007.
        await using (var writeContext = _fixture.CreateContext())
        {
            var repository = new TransactionRepository(writeContext);
            for (var i = 0; i < 100; i++)
            {
                await repository.AddAsync(
                    Transaction.Create(
                        Money.Create(0.01m),
                        TransactionType.Credit,
                        new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero).AddSeconds(i),
                        description: null),
                    CancellationToken.None);
            }

            await new UnitOfWork(writeContext).SaveChangesAsync(CancellationToken.None);
        }

        await using var readContext = _fixture.CreateContext();
        var total = await readContext.Database
            .SqlQuery<decimal>($"SELECT SUM(amount) AS \"Value\" FROM transactions")
            .SingleAsync();

        total.Should().Be(1.00m);
    }
}
