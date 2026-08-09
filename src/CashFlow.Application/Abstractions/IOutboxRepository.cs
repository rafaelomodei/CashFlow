using CashFlow.Application.Outbox;

namespace CashFlow.Application.Abstractions;

/// <summary>
/// Porta do outbox transacional (ADR-004).
/// </summary>
public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Mensagens ainda não publicadas, das mais antigas para as mais novas — a
    /// ordem em que os fatos aconteceram.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);
}
