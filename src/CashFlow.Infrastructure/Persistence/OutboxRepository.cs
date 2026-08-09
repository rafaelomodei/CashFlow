using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Persistência do outbox transacional (ADR-004).
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly CashFlowDbContext _context;

    public OutboxRepository(CashFlowDbContext context) => _context = context;

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await _context.OutboxMessages.AddAsync(message, cancellationToken);

    // Rastreado, ao contrário das leituras de lançamento: quem chama vai marcar
    // as mensagens como processadas e comitar, e sem rastreamento essa marcação
    // não chegaria ao banco.
    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken) =>
        await _context.OutboxMessages
            .Where(message => message.ProcessedAt == null)
            .OrderBy(message => message.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
}
