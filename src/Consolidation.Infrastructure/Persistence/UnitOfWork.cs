using Consolidation.Application.Abstractions;

namespace Consolidation.Infrastructure.Persistence;

/// <summary>
/// Fronteira transacional da consolidação sobre o
/// <see cref="ConsolidationDbContext"/>. O efeito no saldo e a marcação do evento
/// entram no mesmo commit — é o ponto 2 da ADR-007.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ConsolidationDbContext _context;

    public UnitOfWork(ConsolidationDbContext context) => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
