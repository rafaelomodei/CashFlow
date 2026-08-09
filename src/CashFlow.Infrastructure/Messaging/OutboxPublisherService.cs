using CashFlow.Application.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Varre o outbox periodicamente e publica o que estiver pendente (ADR-004).
///
/// Roda **fora** do caminho de registro do lançamento. É essa separação que faz
/// `POST /transactions` responder `201` com o broker fora do ar: aqui o evento
/// espera, lá ninguém espera por ele (RNF-001).
/// </summary>
public sealed class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxPublisherOptions _options;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopes,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxPublisherService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = _options.PollingInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            var succeeded = await RunCycleAsync(stoppingToken);

            // Backoff no ciclo, e não por mensagem: o que costuma falhar é o
            // broker inteiro, não uma mensagem específica — o payload é gerado
            // por nós e não tem por que ser rejeitado sozinho. Guardar o instante
            // da última tentativa de cada linha resolveria um problema que este
            // sistema não tem.
            delay = succeeded
                ? _options.PollingInterval
                : Min(delay * 2, _options.MaxPollingInterval);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <returns>Falso quando o ciclo tentou publicar e nada foi confirmado.</returns>
    private async Task<bool> RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<PublishPendingOutboxMessagesUseCase>();

            var result = await useCase.Handle(_options.BatchSize, stoppingToken);

            if (result.Failed > 0)
            {
                _logger.LogWarning(
                    "Outbox cycle published {Published} of {Attempted} pending messages",
                    result.Published, result.Attempted);
            }
            else if (result.Published > 0)
            {
                _logger.LogInformation(
                    "Outbox cycle published {Published} messages", result.Published);
            }

            return result.Failed == 0;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // O laço não pode morrer: um serviço em segundo plano que encerra em
            // silêncio deixa os eventos pendentes para sempre, e o sintoma
            // aparece só na consolidação, longe daqui.
            _logger.LogError(exception, "Outbox cycle failed");

            return false;
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;
}
