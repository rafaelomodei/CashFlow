using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Dono da conexão e do canal com o broker, e o único lugar que declara a
/// topologia.
///
/// A conexão é aberta **na primeira publicação**, não na inicialização. É o que
/// permite à Cash Flow API subir e registrar lançamentos com o RabbitMQ fora do
/// ar (RNF-001, ADR-009): tornar o broker pré-condição de startup contradiria o
/// Outbox inteiro.
///
/// Pelo mesmo motivo, uma conexão perdida não é erro terminal — a próxima
/// chamada reabre. O broker pode cair e voltar sem que ninguém reinicie nada
/// (RNF-007).
/// </summary>
public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            await DiscardAsync();
            _connection = await CreateConnectionAsync(cancellationToken);

            // Confirmações rastreadas: com elas, `BasicPublishAsync` só retorna
            // depois do ack do broker, e a mensagem do outbox só é marcada como
            // publicada quando de fato foi (ADR-004).
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await DeclareTopologyAsync(_channel, cancellationToken);

            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password,
            RequestedConnectionTimeout = _options.ConnectionTimeout,

            // Padrão do cliente, explícito porque dependemos dele. Medido: com um
            // restart do broker, a recuperação automática sozinha resolve, e a
            // verificação de canal aberto acima também resolve sozinha — desligar
            // as duas é o que faz o teste de recuperação reprovar. A redundância é
            // deliberada: a recuperação automática cobre a queda de rede, e
            // reabrir do zero cobre o canal fechado por erro de protocolo, que ela
            // não recupera.
            AutomaticRecoveryEnabled = true,
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Declarada a cada conexão, e não uma vez no startup: é idempotente no
    /// RabbitMQ, e um broker que voltou de um restart sem volume persistido
    /// precisa da topologia de novo. Declarar de menos custa mensagem perdida;
    /// declarar de novo não custa nada.
    /// </summary>
    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            RabbitMqTopology.DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RabbitMqTopology.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RabbitMqTopology.DeadLetterQueue, RabbitMqTopology.DeadLetterExchange,
            RabbitMqTopology.DeadLetterRoutingKey, cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            RabbitMqTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RabbitMqTopology.Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RabbitMqTopology.DeadLetterExchange,
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RabbitMqTopology.Queue, RabbitMqTopology.Exchange, RabbitMqTopology.RoutingKey,
            cancellationToken: cancellationToken);
    }

    private async Task DiscardAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DiscardAsync();
        _gate.Dispose();
    }
}
