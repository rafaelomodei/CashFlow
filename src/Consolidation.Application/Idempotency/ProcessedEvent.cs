namespace Consolidation.Application.Idempotency;

/// <summary>
/// Marca de que um evento já foi aplicado ao saldo. <see cref="EventId"/> é
/// chave primária no banco: reprocessar deixa de ser uma soma duplicada e passa
/// a ser uma violação de unicidade detectável (ADR-007).
/// </summary>
public sealed record ProcessedEvent(Guid EventId, DateTimeOffset ProcessedAt)
{
    public static ProcessedEvent Now(Guid eventId) => new(eventId, DateTimeOffset.UtcNow);
}
