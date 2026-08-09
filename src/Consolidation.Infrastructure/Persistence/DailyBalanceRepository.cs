using Consolidation.Application.Abstractions;
using Consolidation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.Infrastructure.Persistence;

/// <summary>
/// Persistência do saldo diário (ADR-005). Nenhum método comita: quem fecha a
/// transação é <see cref="UnitOfWork"/> (ADR-007).
/// </summary>
public sealed class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly ConsolidationDbContext _context;

    public DailyBalanceRepository(ConsolidationDbContext context) => _context = context;

    // Rastreado: quem chama vai aplicar o evento sobre o saldo devolvido e
    // comitar. Sem rastreamento, a soma aconteceria só em memória.
    public Task<DailyBalance?> GetAsync(DateOnly date, CancellationToken cancellationToken) =>
        _context.DailyBalances.FirstOrDefaultAsync(balance => balance.Date == date, cancellationToken);

    public async Task AddAsync(DailyBalance balance, CancellationToken cancellationToken) =>
        await _context.DailyBalances.AddAsync(balance, cancellationToken);
}
