namespace Consolidation.Application.Abstractions;

/// <summary>
/// Fronteira transacional da consolidação: o efeito no saldo e a marcação do
/// evento como processado precisam entrar juntos, ou a idempotência não vale
/// nada (ADR-007).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
