using Consolidation.Application.Abstractions;
using Consolidation.Application.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.Infrastructure.Persistence;

/// <summary>
/// Registro de eventos já aplicados ao saldo (ADR-007).
/// </summary>
public sealed class ProcessedEventRepository : IProcessedEventRepository
{
    private readonly ConsolidationDbContext _context;

    public ProcessedEventRepository(ConsolidationDbContext context) => _context = context;

    /// <summary>
    /// Caminho barato, não garantia: entre esta consulta e o commit cabe outro
    /// consumidor. Quem decide é a chave primária.
    /// </summary>
    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken) =>
        _context.ProcessedEvents
            .AsNoTracking()
            .AnyAsync(processedEvent => processedEvent.EventId == eventId, cancellationToken);

    public async Task AddAsync(ProcessedEvent processedEvent, CancellationToken cancellationToken) =>
        await _context.ProcessedEvents.AddAsync(processedEvent, cancellationToken);
}
