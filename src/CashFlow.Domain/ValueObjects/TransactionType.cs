namespace CashFlow.Domain.ValueObjects;

/// <summary>
/// Natureza do lançamento (RN-002). Persistido e serializado pelo nome do
/// contrato — nunca pelo valor numérico —, de modo que reordenar o enum não
/// reinterprete dados já gravados nem eventos já publicados (ADR-013).
/// </summary>
public enum TransactionType
{
    Credit,
    Debit,
}
