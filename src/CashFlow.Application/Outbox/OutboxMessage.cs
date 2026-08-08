namespace CashFlow.Application.Outbox;

/// <summary>
/// Evento aguardando publicação, gravado na mesma transação do lançamento que o
/// originou (ADR-004). Enquanto <see cref="ProcessedAt"/> for nulo, a mensagem
/// está pendente.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage(Guid id, string type, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        Payload = payload;
        OccurredAt = occurredAt;
    }

    /// <summary>Mesmo valor do <c>eventId</c> do envelope — a chave de idempotência.</summary>
    public Guid Id { get; }

    public string Type { get; }

    /// <summary>Envelope serializado, exatamente como será publicado.</summary>
    public string Payload { get; }

    /// <summary>Instante da emissão do evento.</summary>
    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage Create(Guid eventId, string type, string payload, DateTimeOffset occurredAt) =>
        new(eventId, type, payload, occurredAt);

    /// <summary>Chamado apenas após a confirmação do broker (ADR-004).</summary>
    public void MarkAsProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
        Error = null;
    }

    /// <summary>
    /// A mensagem permanece pendente: falhar em publicar não pode fazer o evento
    /// desaparecer (RNF-007).
    /// </summary>
    public void RegisterFailure(string error)
    {
        Attempts++;
        Error = error;
    }
}
