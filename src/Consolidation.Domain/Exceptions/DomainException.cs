namespace Consolidation.Domain.Exceptions;

/// <summary>
/// Raiz das violações de regra de negócio da consolidação. Hierarquia própria,
/// e não compartilhada com o contexto de lançamentos: os dois serviços não
/// dividem código de domínio (ADR-002).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
