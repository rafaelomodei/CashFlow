namespace CashFlow.Application.Abstractions;

/// <summary>
/// Fronteira transacional. Existe para que o caso de uso possa exigir "ou os
/// dois, ou nenhum" sem conhecer o mecanismo que garante isso (ADR-004).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
