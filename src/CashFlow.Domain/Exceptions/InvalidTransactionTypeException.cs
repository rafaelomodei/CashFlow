namespace CashFlow.Domain.Exceptions;

/// <summary>
/// Violação de RN-002: o tipo do lançamento não é <c>CREDIT</c> nem <c>DEBIT</c>.
/// </summary>
public sealed class InvalidTransactionTypeException : DomainException
{
    public InvalidTransactionTypeException() : base("Type must be either CREDIT or DEBIT.")
    {
    }
}
