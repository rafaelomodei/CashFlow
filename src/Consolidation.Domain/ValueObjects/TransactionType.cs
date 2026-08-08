namespace Consolidation.Domain.ValueObjects;

/// <summary>
/// Natureza do lançamento que chega pelo evento (RN-002). O nome do contrato é a
/// representação estável — o valor numérico do enum nunca trafega nem é gravado
/// (ADR-013).
/// </summary>
public enum TransactionType
{
    Credit,
    Debit,
}
