using System.Text;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using RabbitMQ.Client;
using Shared.Contracts;

namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Publica no RabbitMQ conforme `api-contracts.md` §5.4.
///
/// Só retorna depois do *publisher confirm*: quem chama usa esse retorno para
/// marcar a mensagem como publicada, e marcar antes da confirmação transformaria
/// uma falha do broker em evento perdido (ADR-004).
/// </summary>
public sealed class RabbitMqEventPublisher : IEventPublisher
{
    private readonly RabbitMqConnectionProvider _connections;

    public RabbitMqEventPublisher(RabbitMqConnectionProvider connections) => _connections = connections;

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var channel = await _connections.GetChannelAsync(cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.Id.ToString(),
            Type = message.Type,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-event-version"] = TransactionRegisteredEvent.Version,
            },
        };

        // O corpo é o payload gravado no outbox, sem reserializar: o que trafega
        // é exatamente o que ficou auditável na tabela.
        await channel.BasicPublishAsync(
            RabbitMqTopology.Exchange,
            RabbitMqTopology.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);
    }
}
