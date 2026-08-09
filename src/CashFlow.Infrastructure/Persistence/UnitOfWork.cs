using CashFlow.Application.Abstractions;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Implementa a fronteira transacional sobre o <see cref="CashFlowDbContext"/>.
/// O EF Core envolve tudo o que está rastreado em uma única transação de banco,
/// que é exatamente a garantia que ADR-004 exige: ou o lançamento e a mensagem
/// do outbox existem, ou nenhum dos dois existe.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CashFlowDbContext _context;

    public UnitOfWork(CashFlowDbContext context) => _context = context;

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken);
}
