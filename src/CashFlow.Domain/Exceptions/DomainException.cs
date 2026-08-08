namespace CashFlow.Domain.Exceptions;

/// <summary>
/// Raiz das violações de regra de negócio. Existe para que a borda da aplicação
/// distinga o que o cliente enviou errado (400) de uma falha do servidor (500)
/// sem precisar conhecer cada regra individualmente.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
