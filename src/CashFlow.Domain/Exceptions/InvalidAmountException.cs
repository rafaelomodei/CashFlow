namespace CashFlow.Domain.Exceptions;

/// <summary>
/// Violação de RN-001: valor monetário fora do que o domínio aceita.
/// </summary>
public sealed class InvalidAmountException : DomainException
{
    public InvalidAmountException(string message) : base(message)
    {
    }
}
