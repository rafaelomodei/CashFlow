using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using CashFlow.Application.Transactions;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CashFlow.IntegrationTests.Messaging;

/// <summary>
/// O serviço em segundo plano que dá ritmo ao outbox (ADR-004). O que se
/// verifica aqui é o laço: que ele drena sozinho, e que uma falha não o mata.
/// </summary>
[Collection(nameof(MessagingCollection))]
[Trait("Category", "Integration")]
public class OutboxPublisherServiceTests : IAsyncLifetime
{
    private readonly MessagingFixture _fixture;

    public OutboxPublisherServiceTests(MessagingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDatabaseAsync();
        await _fixture.DrainQueueAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Service_ShouldDrainTheOutboxWithoutAnyoneAskingIt()
    {
        await RegisterTransaction(10.00m);
        await RegisterTransaction(20.00m);

        await using var provider = BuildServiceProvider(_fixture.BrokerOptions());
        var service = provider.GetRequiredService<OutboxPublisherService>();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await service.StartAsync(cancellation.Token);
        await WaitUntilOutboxIsEmpty(cancellation.Token);
        await service.StopAsync(CancellationToken.None);

        (await _fixture.DrainQueueAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task Service_ShouldKeepRunningWhenTheBrokerIsUnreachable()
    {
        await RegisterTransaction(30.00m);

        await using var provider = BuildServiceProvider(MessagingFixture.UnreachableBrokerOptions());
        var service = provider.GetRequiredService<OutboxPublisherService>();

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Um serviço em segundo plano que encerra em silêncio deixaria os eventos
        // pendentes para sempre, e o sintoma só apareceria na consolidação, longe
        // daqui. A mensagem continua pendente e com tentativas registradas — não
        // sumiu, e o laço não morreu.
        service.ExecuteTask!.IsCompleted.Should().BeFalse();

        await using var context = _fixture.CreateContext();
        var pending = await context.OutboxMessages.AsNoTracking().SingleAsync();
        pending.ProcessedAt.Should().BeNull();
        pending.Attempts.Should().BeGreaterThan(0);

        await service.StopAsync(CancellationToken.None);
    }

    private ServiceProvider BuildServiceProvider(RabbitMqOptions brokerOptions)
    {
        var services = new ServiceCollection();

        services.AddSingleton(Options.Create(brokerOptions));
        services.AddSingleton(Options.Create(new OutboxPublisherOptions
        {
            PollingInterval = TimeSpan.FromMilliseconds(200),
            MaxPollingInterval = TimeSpan.FromSeconds(1),
        }));
        services.AddSingleton<ILogger<OutboxPublisherService>>(NullLogger<OutboxPublisherService>.Instance);
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddDbContext<CashFlowDbContext>(options => options.UseNpgsql(_fixture.DatabaseConnectionString));
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<PublishPendingOutboxMessagesUseCase>();
        services.AddSingleton<OutboxPublisherService>();

        return services.BuildServiceProvider();
    }

    private async Task WaitUntilOutboxIsEmpty(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var context = _fixture.CreateContext();
            if (!await context.OutboxMessages.AsNoTracking().AnyAsync(m => m.ProcessedAt == null, cancellationToken))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("O outbox não foi drenado dentro do prazo.");
    }

    private async Task RegisterTransaction(decimal amount)
    {
        await using var context = _fixture.CreateContext();

        await new RegisterTransactionUseCase(
                new TransactionRepository(context), new OutboxRepository(context), new UnitOfWork(context))
            .Handle(
                new RegisterTransactionCommand(
                    amount, "CREDIT", new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), null, Guid.NewGuid()),
                CancellationToken.None);
    }
}
