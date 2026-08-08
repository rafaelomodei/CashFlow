namespace Shared.Contracts;

/// <summary>
/// Único evento de integração do sistema (`api-contracts.md` §5). É o contrato
/// mais caro de mudar do projeto: atravessa o outbox, o broker e o worker.
/// </summary>
public sealed record TransactionRegisteredEvent
{
    public const string Type = "TransactionRegistered";

    public const int Version = 1;

    /// <summary>Chave de idempotência do consumidor (ADR-007).</summary>
    public required Guid EventId { get; init; }

    public string EventType => Type;

    public int EventVersion => Version;

    /// <summary>Momento da <b>emissão</b> do evento — não do fato econômico.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    public required Guid CorrelationId { get; init; }

    public required TransactionRegisteredData Data { get; init; }
}

/// <summary>
/// Corpo do evento. O <c>OccurredAt</c> daqui é o do <b>fato econômico</b>, e é
/// ele que determina o dia da consolidação (RN-004). Confundi-lo com o do
/// envelope colocaria todo lançamento retroativo no dia da emissão.
/// </summary>
public sealed record TransactionRegisteredData
{
    public required Guid TransactionId { get; init; }

    /// <summary><c>CREDIT</c> ou <c>DEBIT</c>, sempre como texto (ADR-013).</summary>
    public required string Type { get; init; }

    /// <summary>Sempre positivo, com duas casas decimais.</summary>
    public required decimal Amount { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
