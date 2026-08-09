namespace Consolidation.Infrastructure.Messaging;

/// <summary>
/// Política de consumo e de retry (ADR-003).
/// </summary>
public sealed class TransactionConsumerOptions
{
    public const string SectionName = "TransactionConsumer";

    /// <summary>Tentativas por mensagem antes da DLQ, incluindo a primeira.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Espera entre tentativas. Precisa ser real: `nack` com `requeue=true`
    /// reentrega de imediato e produz o laço quente que a ADR-003 descreve.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Quantas mensagens o broker entrega sem esperar ack. Limita a concorrência
    /// e impede que a fila inteira seja puxada para a memória do worker.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 20;
}
