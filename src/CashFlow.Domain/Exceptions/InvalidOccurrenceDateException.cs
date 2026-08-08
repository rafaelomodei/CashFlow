namespace CashFlow.Domain.Exceptions;

/// <summary>
/// Violação de RN-004: sem data de ocorrência não há dia ao qual consolidar o
/// lançamento.
/// </summary>
public sealed class InvalidOccurrenceDateException : DomainException
{
    public InvalidOccurrenceDateException() : base("OccurredAt is required.")
    {
    }
}
