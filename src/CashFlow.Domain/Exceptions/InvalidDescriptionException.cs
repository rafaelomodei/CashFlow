namespace CashFlow.Domain.Exceptions;

/// <summary>
/// Violação do limite de tamanho de <c>description</c> (premissa P-10).
/// </summary>
public sealed class InvalidDescriptionException : DomainException
{
    public InvalidDescriptionException(string message) : base(message)
    {
    }
}
