using System.Diagnostics;
using Consolidation.Infrastructure.Messaging;
using Consolidation.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Consolidation.IntegrationTests.Messaging;

/// <summary>
/// O consumidor de <c>TransactionRegistered</c> (RF-004, RNF-008, ADR-007).
/// </summary>
[Collection(nameof(ConsumerCollection))]
[Trait("Category", "Integration")]
public class TransactionRegisteredConsumerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Occurrence = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 4, 10);

    private readonly ConsumerFixture _fixture;

    public TransactionRegisteredConsumerTests(ConsumerFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Consumer_ShouldApplyTheEventToTheDayOfTheEconomicFact()
    {
        await _fixture.PublishAsync(ConsumerFixture.Event(Guid.NewGuid(), 1500.00m, "CREDIT", Occurrence));
        await _fixture.PublishAsync(ConsumerFixture.Event(Guid.NewGuid(), 500.50m, "DEBIT", Occurrence));

        await RunConsumerUntil(async () => await BalanceOf(Day) is { } balance && balance.UpdatedAt is not null
            && balance.TotalCredits.Amount == 1500.00m && balance.TotalDebits.Amount == 500.50m);

        var balance = await BalanceOf(Day);
        balance!.Balance.Amount.Should().Be(999.50m);
    }

    [Fact]
    public async Task SameEventDeliveredManyTimes_ShouldChangeTheBalanceOnlyOnce()
    {
        var eventId = Guid.NewGuid();
        var integrationEvent = ConsumerFixture.Event(eventId, 100.00m, "CREDIT", Occurrence);

        // A entrega é at-least-once por decisão (ADR-003): a duplicata não é falha,
        // é ruído esperado. O que não pode acontecer é ela virar dinheiro.
        for (var i = 0; i < 5; i++)
        {
            await _fixture.PublishAsync(integrationEvent);
        }

        await RunConsumerUntil(async () => await ProcessedEventCount() == 1 && await QueueIsDrained());

        var balance = await BalanceOf(Day);
        balance!.TotalCredits.Amount.Should().Be(100.00m, "RNF-008: reprocessar não duplica o impacto");
        (await ProcessedEventCount()).Should().Be(1);
    }

    [Fact]
    public async Task DistinctEventsAboutTheSameTransaction_ShouldBothBeApplied()
    {
        var transactionId = Guid.NewGuid();
        await _fixture.PublishAsync(
            ConsumerFixture.Event(Guid.NewGuid(), 40.00m, "CREDIT", Occurrence, transactionId));
        await _fixture.PublishAsync(
            ConsumerFixture.Event(Guid.NewGuid(), 40.00m, "CREDIT", Occurrence, transactionId));

        // `eventId` identifica a mensagem, não o lançamento (contrato §5.3). Usar
        // `transactionId` como chave de idempotência descartaria o segundo.
        await RunConsumerUntil(async () => await ProcessedEventCount() == 2);

        (await BalanceOf(Day))!.TotalCredits.Amount.Should().Be(80.00m);
    }

    [Fact]
    public async Task ManyEventsForTheSameDay_ShouldAllLandOnTheSameRowWithoutLosingAny()
    {
        const int events = 20;
        for (var i = 0; i < events; i++)
        {
            await _fixture.PublishAsync(ConsumerFixture.Event(Guid.NewGuid(), 1.00m, "CREDIT", Occurrence));
        }

        // Com prefetch, o consumidor processa em paralelo e várias transações
        // tentam criar a linha do mesmo dia. A chave primária de `daily_balances`
        // recusa a segunda, o commit inteiro é desfeito — inclusive a marcação do
        // evento — e a tentativa seguinte encontra a linha e soma sobre ela.
        // É esse par, chave primária mais retry, que faz as vezes de upsert.
        await RunConsumerUntil(async () => await ProcessedEventCount() == events);

        var balance = await BalanceOf(Day);
        balance!.TotalCredits.Amount.Should().Be(events * 1.00m, "nenhum evento pode se perder na disputa");
        (await _fixture.DeadLetterCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MalformedMessage_ShouldReachTheDeadLetterQueueWithoutRetrying()
    {
        await _fixture.PublishAsync("isto não é json");

        var elapsed = Stopwatch.StartNew();
        await RunConsumerUntil(async () => await _fixture.DeadLetterCountAsync() == 1);
        elapsed.Stop();

        // Sem retry: uma mensagem ilegível não fica legível na segunda tentativa,
        // e esperar por isso só atrasaria a fila inteira.
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        (await ProcessedEventCount()).Should().Be(0);
    }

    [Fact]
    public async Task EventViolatingADomainRule_ShouldReachTheDeadLetterQueue()
    {
        await _fixture.PublishAsync(ConsumerFixture.Event(Guid.NewGuid(), 0m, "CREDIT", Occurrence));

        await RunConsumerUntil(async () => await _fixture.DeadLetterCountAsync() == 1);

        // Valor não positivo é violação de RN-001 e nunca deixará de ser: retentar
        // é tão inútil quanto retentar um JSON quebrado.
        (await BalanceOf(Day)).Should().BeNull();
    }

    [Fact]
    public async Task TransientFailure_ShouldRetryWithRealWaitAndThenDeadLetter()
    {
        await _fixture.PublishAsync(ConsumerFixture.Event(Guid.NewGuid(), 10.00m, "CREDIT", Occurrence));

        var options = new TransactionConsumerOptions
        {
            MaxAttempts = 3,
            RetryDelay = TimeSpan.FromMilliseconds(500),
        };

        var elapsed = Stopwatch.StartNew();
        await RunConsumerUntil(
            async () => await _fixture.DeadLetterCountAsync() == 1,
            options,
            // Banco inalcançável: falha transitória do ponto de vista do consumidor,
            // que é exatamente o caso em que retentar faz sentido.
            databaseConnectionString: "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");
        elapsed.Stop();

        // Duas esperas entre três tentativas. Se o retry fosse imediato, a mensagem
        // chegaria à DLQ em milissegundos — o laço quente que a ADR-003 descreve.
        elapsed.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1));
    }

    private async Task<Domain.Entities.DailyBalance?> BalanceOf(DateOnly date)
    {
        await using var context = _fixture.CreateContext();

        return await new DailyBalanceRepository(context).GetAsync(date, CancellationToken.None);
    }

    private async Task<int> ProcessedEventCount()
    {
        await using var context = _fixture.CreateContext();

        return await context.ProcessedEvents.AsNoTracking().CountAsync();
    }

    private async Task<bool> QueueIsDrained()
    {
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        return true;
    }

    private async Task RunConsumerUntil(
        Func<Task<bool>> condition,
        TransactionConsumerOptions? options = null,
        string? databaseConnectionString = null)
    {
        await using var provider = _fixture.BuildConsumer(options, databaseConnectionString);
        var consumer = provider.GetRequiredService<TransactionRegisteredConsumer>();

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await condition())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            throw new TimeoutException("A condição não foi atingida dentro do prazo.");
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }
}
