namespace CashFlow.Application.Transactions;

/// <summary>
/// Página de lançamentos. Não existe total de registros: contá-lo custaria um
/// <c>COUNT(*)</c> a cada página, que é exatamente o custo O(n) que a paginação
/// por cursor foi escolhida para evitar (ADR-014).
/// </summary>
public sealed record TransactionPage(
    IReadOnlyList<TransactionDto> Items,
    string? NextCursor,
    bool HasMore);
