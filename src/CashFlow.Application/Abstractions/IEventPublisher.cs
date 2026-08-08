using CashFlow.Application.Outbox;

namespace CashFlow.Application.Abstractions;

/// <summary>
/// Porta de publicação no broker. Só o publisher do outbox depende dela — o
/// registro de lançamento não, e é isso que sustenta RNF-001.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Retorna quando o broker confirmar o recebimento. Sem confirmação, a
    /// mensagem não pode ser marcada como publicada (ADR-004).
    /// </summary>
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
