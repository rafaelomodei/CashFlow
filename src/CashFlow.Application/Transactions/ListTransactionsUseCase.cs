using CashFlow.Application.Abstractions;
using CashFlow.Application.Exceptions;
using CashFlow.Domain.Entities;

namespace CashFlow.Application.Transactions;

/// <summary>
/// UC-03 — listar lançamentos com paginação por cursor e filtro por período
/// (RF-003, ADR-014).
/// </summary>
public sealed class ListTransactionsUseCase
{
    private const int DefaultLimit = 50;
    private const int MinimumLimit = 1;
    private const int MaximumLimit = 200;

    private readonly ITransactionRepository _transactions;

    public ListTransactionsUseCase(ITransactionRepository transactions) => _transactions = transactions;

    public async Task<TransactionPage> Handle(ListTransactionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = ResolveLimit(query.Limit);
        EnsurePeriodIsCoherent(query);
        var cursor = query.Cursor is null ? null : TransactionCursor.Decode(query.Cursor);

        var filter = new TransactionListFilter(
            From: StartOfDay(query.StartDate),
            ToExclusive: StartOfNextDay(query.EndDate),
            CursorOccurredAt: cursor?.OccurredAt,
            CursorId: cursor?.Id,
            // Um registro além do pedido: é o que distingue "acabou" de "a página
            // encheu", sem custar um COUNT(*).
            Limit: limit + 1);

        var found = await _transactions.ListAsync(filter, cancellationToken);

        var hasMore = found.Count > limit;
        IReadOnlyList<Transaction> items = hasMore ? found.Take(limit).ToList() : found;
        var last = items.Count > 0 ? items[^1] : null;

        return new TransactionPage(
            items.Select(TransactionDto.From).ToList(),
            hasMore && last is not null ? new TransactionCursor(last.OccurredAt, last.Id).Encode() : null,
            hasMore);
    }

    private static int ResolveLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        if (limit is < MinimumLimit or > MaximumLimit)
        {
            throw new InvalidQueryException($"Limit must be between {MinimumLimit} and {MaximumLimit}.");
        }

        return limit.Value;
    }

    private static void EnsurePeriodIsCoherent(ListTransactionsQuery query)
    {
        if (query.StartDate is not null && query.EndDate is not null && query.StartDate > query.EndDate)
        {
            throw new InvalidQueryException("StartDate must not be later than endDate.");
        }
    }

    private static DateTimeOffset? StartOfDay(DateOnly? date) =>
        date is null ? null : new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    /// <summary>
    /// <c>endDate</c> é inclusivo: o corte fica no começo do dia seguinte, senão
    /// tudo que ocorreu depois da meia-noite do próprio dia ficaria de fora — o
    /// erro de intervalo mais comum, e silencioso.
    /// </summary>
    private static DateTimeOffset? StartOfNextDay(DateOnly? date) =>
        date is null ? null : new DateTimeOffset(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
