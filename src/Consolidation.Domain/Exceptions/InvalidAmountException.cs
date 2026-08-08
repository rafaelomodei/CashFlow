namespace Consolidation.Domain.Exceptions;

/// <summary>
/// Valor monetário que a consolidação não aceita — seja por escala, por faixa ou
/// por não ser positivo onde a regra exige que seja (RN-001).
/// </summary>
public sealed class InvalidAmountException : DomainException
{
    public InvalidAmountException(string message) : base(message)
    {
    }
}
