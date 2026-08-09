using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Contracts;

namespace Consolidation.Infrastructure.Messaging;

/// <summary>
/// Conexão e canal do lado consumidor, e o único lugar que declara a topologia
/// aqui.
///
/// O consumidor também declara, e não apenas o produtor: os dois serviços sobem
/// em ordem indefinida, e quem chegar primeiro precisa encontrar — ou criar — a
/// fila. A declaração é idempotente no RabbitMQ, então declarar dos dois lados
/// não custa nada e cobre as duas ordens de inicialização.
///
/// Sem confirmação de publicação: quem publica é o outro contexto.
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
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

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
            AutomaticRecoveryEnabled = true,
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }

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
