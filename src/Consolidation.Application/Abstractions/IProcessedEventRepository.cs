using Consolidation.Application.Idempotency;

namespace Consolidation.Application.Abstractions;

/// <summary>
/// Porta da idempotência do consumidor (ADR-007).
/// </summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Caminho barato: evita o trabalho quando a reentrega é evidente. A garantia
    /// de fato é a chave primária de <c>processed_events</c> — duas mensagens
    /// concorrentes podem passar por aqui, e só uma sobrevive ao commit.
    /// </summary>
    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken);

    Task AddAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken);
}
