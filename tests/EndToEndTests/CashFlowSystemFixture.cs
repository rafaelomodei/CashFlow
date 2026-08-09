using System.Net;
using System.Net.Sockets;
using Consolidation.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace EndToEndTests;

/// <summary>
/// O sistema inteiro: dois bancos, um broker, as duas APIs e o worker.
///
/// É a única montagem do projeto em que os dois contextos coexistem, e ela existe
/// para verificar exatamente aquilo que nenhum dos dois lados pode verificar
/// sozinho — que o lançamento registrado de um lado vira saldo do outro, sem que
/// eles se conheçam.
/// </summary>
public sealed class CashFlowSystemFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _cashFlowDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly PostgreSqlContainer _consolidationDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly int _brokerPort = FreeTcpPort();
    private readonly RabbitMqContainer _broker;

    private WebApplicationFactory<CashFlow.Api.Transactions.TransactionsController>? _cashFlowApi;
    private WebApplicationFactory<Consolidation.Api.Balances.DailyBalancesController>? _consolidationApi;
    private IHost? _worker;

    public CashFlowSystemFixture()
    {
        _broker = new RabbitMqBuilder("rabbitmq:4.3-management-alpine")
            .WithPortBinding(_brokerPort, 5672)
            .Build();
    }

    public HttpClient CashFlow { get; private set; } = null!;

    public HttpClient Consolidation { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_cashFlowDb.StartAsync(), _consolidationDb.StartAsync(), _broker.StartAsync());

        _cashFlowApi = new WebApplicationFactory<CashFlow.Api.Transactions.TransactionsController>()
            .WithWebHostBuilder(builder => Configure(builder, new Dictionary<string, string?>
            {
                ["ConnectionStrings:CashFlowDb"] = _cashFlowDb.GetConnectionString(),
                ["OutboxPublisher:PollingInterval"] = "00:00:00.200",
            }));

        _consolidationApi = new WebApplicationFactory<Consolidation.Api.Balances.DailyBalancesController>()
            .WithWebHostBuilder(builder => Configure(builder, new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConsolidationDb"] = _consolidationDb.GetConnectionString(),
            }));

        CashFlow = _cashFlowApi.CreateClient();

        // A API de consolidação precisa subir antes do worker: é ela que aplica as
        // migrations do `consolidation_db`.
        Consolidation = _consolidationApi.CreateClient();

        _worker = BuildWorker();
        await _worker.StartAsync();
    }

    private void Configure(IWebHostBuilder builder, Dictionary<string, string?> settings)
    {
        settings["RabbitMq:Host"] = "127.0.0.1";
        settings["RabbitMq:Port"] = _brokerPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        settings["RabbitMq:Username"] = "rabbitmq";
        settings["RabbitMq:Password"] = "rabbitmq";

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    private IHost BuildWorker()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConsolidationDb"] = _consolidationDb.GetConnectionString(),
            ["RabbitMq:Host"] = "127.0.0.1",
            ["RabbitMq:Port"] = _brokerPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RabbitMq:Username"] = "rabbitmq",
            ["RabbitMq:Password"] = "rabbitmq",
            ["TransactionConsumer:RetryDelay"] = "00:00:00.200",
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddConsolidationPersistence(builder.Configuration);
        builder.Services.AddConsolidationConsumer(builder.Configuration);

        return builder.Build();
    }

    private static int FreeTcpPort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    public async Task StopConsolidationAsync()
    {
        if (_worker is not null)
        {
            await _worker.StopAsync();
            _worker.Dispose();
            _worker = null;
        }

        _consolidationApi?.Dispose();
        _consolidationApi = null;
    }

    public async Task DisposeAsync()
    {
        if (_worker is not null)
        {
            await _worker.StopAsync();
            _worker.Dispose();
        }

        _cashFlowApi?.Dispose();
        _consolidationApi?.Dispose();

        await Task.WhenAll(
            _cashFlowDb.DisposeAsync().AsTask(),
            _consolidationDb.DisposeAsync().AsTask(),
            _broker.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(nameof(CashFlowSystemCollection))]
public sealed class CashFlowSystemCollection : ICollectionFixture<CashFlowSystemFixture>;
