using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;

namespace CashFlow.Application.Abstractions;

/// <summary>
/// Porta de persistência de lançamentos. Nenhum método comita: a atomicidade
/// entre lançamento e outbox é responsabilidade de <see cref="IUnitOfWork"/>
/// (ADR-004).
/// </summary>
public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken);

    /// <summary>Nulo quando o lançamento não existe — ausência não é erro (UC-06).</summary>
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Página em <c>occurred_at DESC, id DESC</c> a partir da posição do filtro,
    /// com no máximo <c>filter.Limit</c> registros (ADR-014).
    /// </summary>
    Task<IReadOnlyList<Transaction>> ListAsync(TransactionListFilter filter, CancellationToken cancellationToken);
}
