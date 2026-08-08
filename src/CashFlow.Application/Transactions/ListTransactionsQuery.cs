namespace CashFlow.Application.Transactions;

/// <summary>
/// Entrada de UC-03, espelhando os parâmetros de `GET /transactions`. Os filtros
/// não viajam dentro do cursor: eles são reenviados a cada página
/// (`api-contracts.md` §2.3).
/// </summary>
public sealed record ListTransactionsQuery(
    int? Limit,
    string? Cursor,
    DateOnly? StartDate,
    DateOnly? EndDate);
