using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Consolidation.Application.Abstractions;
using Consolidation.Application.Balances;
using Consolidation.Infrastructure.Messaging;
using Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Contracts;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Consolidation.IntegrationTests.Messaging;

/// <summary>
/// Banco e broker reais para o lado consumidor. O consumo é assíncrono por
/// natureza: um dublê de broker responderia na hora e esconderia justamente o
/// que precisa ser verificado — ack manual, reentrega e DLQ.
/// </summary>
public sealed class ConsumerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly int _brokerPort = FreeTcpPort();
    private readonly RabbitMqContainer _broker;

    public ConsumerFixture()
    {
        _broker = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
            .WithPortBinding(_brokerPort, 5672)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _broker.StartAsync());

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    private static int FreeTcpPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    public string DatabaseConnectionString => _database.GetConnectionString();

    public ConsolidationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseNpgsql(DatabaseConnectionString)
            .Options);

    public RabbitMqOptions BrokerOptions() => new()
    {
        Host = "127.0.0.1",
        Port = _brokerPort,
        Username = "rabbitmq",
        Password = "rabbitmq",
        ConnectionTimeout = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// Publica direto no exchange, no lugar do outbox: o que este conjunto de
    /// testes verifica é o consumo, e amarrá-lo ao produtor faria uma falha de um
    /// lado reprovar o teste do outro.
    /// </summary>
    public async Task PublishAsync(string payload, Guid? correlationId = null)
    {
        await using var provider = new RabbitMqConnectionProvider(Options.Create(BrokerOptions()));
        var channel = await provider.GetChannelAsync(CancellationToken.None);

        await channel.BasicPublishAsync(
            RabbitMqTopology.Exchange,
            RabbitMqTopology.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = (correlationId ?? Guid.NewGuid()).ToString(),
            },
            body: Encoding.UTF8.GetBytes(payload));
    }

    public Task PublishAsync(TransactionRegisteredEvent integrationEvent) =>
        PublishAsync(
            JsonSerializer.Serialize(integrationEvent, IntegrationEvents.SerializerOptions),
            integrationEvent.CorrelationId);

    public static TransactionRegisteredEvent Event(
        Guid eventId, decimal amount, string type, DateTimeOffset occurredAt, Guid? transactionId = null) =>
        new()
        {
            EventId = eventId,
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid(),
            Data = new TransactionRegisteredData
            {
                TransactionId = transactionId ?? Guid.NewGuid(),
                Type = type,
                Amount = amount,
                OccurredAt = occurredAt,
            },
        };

    /// <param name="databaseConnectionString">
    /// Aberto para que um teste possa apontar o consumidor para um banco
    /// inalcançável e exercitar o caminho de falha transitória.
    /// </param>
    public ServiceProvider BuildConsumer(
        TransactionConsumerOptions? consumerOptions = null,
        string? databaseConnectionString = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(Options.Create(BrokerOptions()));
        services.AddSingleton(Options.Create(consumerOptions ?? new TransactionConsumerOptions
        {
            MaxAttempts = 3,
            RetryDelay = TimeSpan.FromMilliseconds(200),
        }));
        services.AddSingleton<ILogger<TransactionRegisteredConsumer>>(
            NullLogger<TransactionRegisteredConsumer>.Instance);
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddDbContext<ConsolidationDbContext>(options =>
            options.UseNpgsql(databaseConnectionString ?? DatabaseConnectionString));
        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ConsolidateTransactionUseCase>();
        services.AddSingleton<TransactionRegisteredConsumer>();

        return services.BuildServiceProvider();
    }

    public async Task<int> DeadLetterCountAsync()
    {
        await using var provider = new RabbitMqConnectionProvider(Options.Create(BrokerOptions()));
        var channel = await provider.GetChannelAsync(CancellationToken.None);

        return (int)await channel.MessageCountAsync(RabbitMqTopology.DeadLetterQueue);
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE daily_balances, processed_events");

        await using var provider = new RabbitMqConnectionProvider(Options.Create(BrokerOptions()));
        var channel = await provider.GetChannelAsync(CancellationToken.None);
        await channel.QueuePurgeAsync(RabbitMqTopology.Queue);
        await channel.QueuePurgeAsync(RabbitMqTopology.DeadLetterQueue);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await _broker.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ConsumerCollection))]
public sealed class ConsumerCollection : ICollectionFixture<ConsumerFixture>;
