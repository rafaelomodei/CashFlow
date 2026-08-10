using System.Net;
using System.Net.Sockets;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Contracts;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace CashFlow.IntegrationTests.Messaging;

/// <summary>
/// Banco e broker reais, nas mesmas imagens do <c>docker-compose.yml</c>. O
/// broker precisa ser real aqui por um motivo específico: o que estes testes
/// verificam é o comportamento quando ele **não** responde, e um dublê que falha
/// sob comando prova apenas que o dublê obedece.
/// </summary>
public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine").Build();

    /// <summary>
    /// Porta fixa, e não a aleatória que o Testcontainers atribui. `StopAsync`
    /// remove o container e `StartAsync` cria outro, com outro mapeamento — e
    /// nenhum broker real muda de endereço ao reiniciar. Sem fixá-la, o teste de
    /// recuperação estaria medindo um artefato do harness em vez da reconexão.
    /// </summary>
    private readonly int _brokerPort = FreeTcpPort();

    private readonly RabbitMqContainer _broker;

    public MessagingFixture()
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

    public CashFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseNpgsql(DatabaseConnectionString)
            .Options);

    /// <summary>Opções apontando para o broker real, em endereço estável.</summary>
    public RabbitMqOptions BrokerOptions() => new()
    {
        Host = "127.0.0.1",
        Port = _brokerPort,
        Username = "rabbitmq",
        Password = "rabbitmq",
        ConnectionTimeout = TimeSpan.FromSeconds(5),
    };

    /// <summary>
    /// Opções apontando para uma porta onde nada escuta. Usado onde o teste
    /// precisa de uma falha de conexão sem parar o container compartilhado.
    /// </summary>
    public static RabbitMqOptions UnreachableBrokerOptions() => new()
    {
        Host = "127.0.0.1",
        Port = 1,
        Username = "rabbitmq",
        Password = "rabbitmq",
        ConnectionTimeout = TimeSpan.FromSeconds(2),
    };

    public RabbitMqConnectionProvider CreateConnectionProvider(RabbitMqOptions options) =>
        new(Options.Create(options));

    public async Task StopBrokerAsync() => await _broker.StopAsync();

    /// <summary>
    /// Sobe o broker e só devolve quando ele aceita conexão. Esperar aqui não
    /// enfraquece o teste: na aplicação quem espera é o backoff do publisher, que
    /// simplesmente tenta de novo no ciclo seguinte. O que o teste precisa medir
    /// é se o publisher se reconecta, não se ele adivinha o instante em que o
    /// broker terminou de subir.
    /// </summary>
    public async Task StartBrokerAsync()
    {
        await _broker.StartAsync();

        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var probe = CreateConnectionProvider(BrokerOptions());
                await probe.GetChannelAsync(CancellationToken.None);

                return;
            }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("O broker não voltou a aceitar conexões dentro do prazo.");
    }

    /// <summary>
    /// Desliga a fila do exchange e religa ao final. Serve a um cenário só: uma
    /// mensagem publicada sem destino é aceita pelo exchange e descartada em
    /// silêncio — é a forma mais discreta de perder evento que existe.
    /// </summary>
    /// <param name="provider">
    /// Precisa ser o mesmo provider que vai publicar. Um provider novo
    /// redeclararia a topologia ao conectar e religaria a fila antes do teste
    /// chegar a publicar.
    /// </param>
    public static async Task WithoutQueueBindingAsync(RabbitMqConnectionProvider provider, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(action);

        var channel = await provider.GetChannelAsync(CancellationToken.None);

        await channel.QueueUnbindAsync(RabbitMqTopology.Queue, RabbitMqTopology.Exchange, RabbitMqTopology.RoutingKey);
        try
        {
            await action();
        }
        finally
        {
            await channel.QueueBindAsync(
                RabbitMqTopology.Queue, RabbitMqTopology.Exchange, RabbitMqTopology.RoutingKey);
        }
    }

    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE transactions, outbox_messages");
    }

    /// <summary>Drena a fila e devolve o que estava nela.</summary>
    public async Task<IReadOnlyList<BasicGetResult>> DrainQueueAsync()
    {
        await using var provider = CreateConnectionProvider(BrokerOptions());
        var channel = await provider.GetChannelAsync(CancellationToken.None);

        var messages = new List<BasicGetResult>();
        while (await channel.BasicGetAsync(RabbitMqTopology.Queue, autoAck: true) is { } message)
        {
            messages.Add(message);
        }

        return messages;
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        await _broker.DisposeAsync();
    }
}

[CollectionDefinition(nameof(MessagingCollection))]
public sealed class MessagingCollection : ICollectionFixture<MessagingFixture>;
