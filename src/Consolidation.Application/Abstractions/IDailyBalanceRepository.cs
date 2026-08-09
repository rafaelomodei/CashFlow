using Consolidation.Domain.Entities;

namespace Consolidation.Application.Abstractions;

/// <summary>
/// Porta de persistência do saldo diário. Nenhum método comita: a atomicidade
/// entre o efeito no saldo e a marcação do evento é de <see cref="IUnitOfWork"/>
/// (ADR-007).
/// </summary>
public interface IDailyBalanceRepository
{
    /// <summary>Nulo quando o dia ainda não foi consolidado.</summary>
    Task<DailyBalance?> GetAsync(DateOnly date, CancellationToken cancellationToken);

    Task AddAsync(DailyBalance balance, CancellationToken cancellationToken);
}
