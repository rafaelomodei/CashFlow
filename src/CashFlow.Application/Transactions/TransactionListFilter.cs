namespace CashFlow.Application.Transactions;

/// <summary>
/// Consulta já traduzida para o vocabulário do repositório: intervalo resolvido
/// em instantes, posição do cursor aberta em seus dois campos e limite já
/// acrescido do registro extra que revela se há página seguinte.
/// </summary>
/// <param name="ToExclusive">
/// Exclusivo de propósito: <c>endDate</c> é inclusivo no contrato, e o dia
/// inteiro só cabe comparando contra o começo do dia seguinte.
/// </param>
public sealed record TransactionListFilter(
    DateTimeOffset? From,
    DateTimeOffset? ToExclusive,
    DateTimeOffset? CursorOccurredAt,
    Guid? CursorId,
    int Limit);
