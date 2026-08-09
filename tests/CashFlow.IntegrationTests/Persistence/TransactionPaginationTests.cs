using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// Paginação por cursor e filtro por período contra o banco real (RF-003,
/// ADR-014).
///
/// Os testes passam pelo caso de uso, e não direto pelo repositório: é a
/// composição que vai para produção — cursor codificado, <c>limit + 1</c> e
/// comparação de tupla no SQL —, e é nela que um erro de tradução aparece.
/// </summary>
[Collection(nameof(CashFlowDatabaseCollection))]
[Trait("Category", "Integration")]
public class TransactionPaginationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Day = new(2026, 4, 10, 0, 0, 0, TimeSpan.Zero);

    private readonly CashFlowDatabaseFixture _fixture;

    public TransactionPaginationTests(CashFlowDatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Pages_ShouldCoverEveryTransactionExactlyOnceInDescendingOrder()
    {
        var persisted = await Persist(
            Day.AddHours(9),
            Day.AddHours(10),
            Day.AddHours(11),
            Day.AddHours(12),
            Day.AddHours(13));

        var walked = await WalkAllPages(limit: 2);

        walked.Should().Equal(ExpectedOrder(persisted));
    }

    [Fact]
    public async Task Pages_ShouldBreakTiesByIdWhenOccurredAtIsIdentical()
    {
        // Sem desempate por id a ordem entre estes três é indefinida, e a
        // paginação passa a pular e repetir sem nenhum sinal de erro (ADR-014).
        var persisted = await Persist(Day.AddHours(9), Day.AddHours(9), Day.AddHours(9));

        var walked = await WalkAllPages(limit: 1);

        walked.Should().Equal(ExpectedOrder(persisted));
    }

    [Fact]
    public async Task LastPage_ShouldReportNoMore()
    {
        await Persist(Day.AddHours(9), Day.AddHours(10));

        var page = await Query(new ListTransactionsQuery(Limit: 5, Cursor: null, StartDate: null, EndDate: null));

        page.Items.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Period_ShouldIncludeBothEndsOfTheInterval()
    {
        var persisted = await Persist(
            Day.AddDays(-1).AddHours(23),
            Day.AddHours(0),
            Day.AddHours(23).AddMinutes(59),
            Day.AddDays(1).AddHours(0));

        var page = await Query(new ListTransactionsQuery(
            Limit: null,
            Cursor: null,
            StartDate: DateOnly.FromDateTime(Day.UtcDateTime),
            EndDate: DateOnly.FromDateTime(Day.UtcDateTime)));

        // O dia inteiro, incluindo 23h59 — o corte no começo do dia seguinte é o
        // que impede que a maior parte do último dia fique de fora.
        page.Items.Select(item => item.Id).Should().Equal(persisted[2].Id, persisted[1].Id);
    }

    [Fact]
    public async Task EmptyRange_ShouldReturnAnEmptyPageInsteadOfAnError()
    {
        await Persist(Day.AddHours(9));

        var page = await Query(new ListTransactionsQuery(
            Limit: null,
            Cursor: null,
            StartDate: new DateOnly(2020, 1, 1),
            EndDate: new DateOnly(2020, 1, 2)));

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentInsertion_ShouldNotDuplicateOrDropRowsBetweenPages()
    {
        var persisted = await Persist(Day.AddHours(9), Day.AddHours(10), Day.AddHours(11));

        var first = await Query(new ListTransactionsQuery(Limit: 2, Cursor: null, StartDate: null, EndDate: null));

        // Um lançamento novo entra no topo da lista entre as duas páginas. Com
        // OFFSET, ele empurraria todos os itens uma posição para baixo e o
        // último item da primeira página reapareceria na segunda — em uma lista
        // financeira, indistinguível de um lançamento duplicado.
        await Persist(Day.AddHours(23));

        var second = await Query(new ListTransactionsQuery(
            Limit: 2, Cursor: first.NextCursor, StartDate: null, EndDate: null));

        second.Items.Select(item => item.Id).Should().Equal(persisted[0].Id);
        first.Items.Concat(second.Items).Select(item => item.Id).Should().OnlyHaveUniqueItems();
    }

    private static IReadOnlyList<Guid> ExpectedOrder(IReadOnlyList<Transaction> persisted) =>
        [.. persisted
            .OrderByDescending(transaction => transaction.OccurredAt)
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction => transaction.Id)];

    private async Task<IReadOnlyList<Transaction>> Persist(params DateTimeOffset[] occurrences)
    {
        var transactions = occurrences
            .Select(occurredAt => Transaction.Create(
                Money.Create(100.00m),
                TransactionType.Credit,
                occurredAt,
                description: null))
            .ToList();

        await using var context = _fixture.CreateContext();
        var repository = new TransactionRepository(context);
        foreach (var transaction in transactions)
        {
            await repository.AddAsync(transaction, CancellationToken.None);
        }

        await new UnitOfWork(context).SaveChangesAsync(CancellationToken.None);

        return transactions;
    }

    private async Task<TransactionPage> Query(ListTransactionsQuery query)
    {
        await using var context = _fixture.CreateContext();

        return await new ListTransactionsUseCase(new TransactionRepository(context))
            .Handle(query, CancellationToken.None);
    }

    private async Task<List<Guid>> WalkAllPages(int limit)
    {
        var visited = new List<Guid>();
        string? cursor = null;

        do
        {
            var page = await Query(new ListTransactionsQuery(
                Limit: limit, Cursor: cursor, StartDate: null, EndDate: null));
            visited.AddRange(page.Items.Select(item => item.Id));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        return visited;
    }
}
