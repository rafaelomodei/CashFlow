using System.Data.Common;
using System.Text;
using System.Text.Json;
using Consolidation.Application.Balances;
using Consolidation.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace Consolidation.Infrastructure.Messaging;

/// <summary>
/// Consome <c>TransactionRegistered</c> e aplica o lançamento ao saldo do dia
/// (RF-004, ADR-003, ADR-007). O `ack` é manual e só acontece depois do commit.
/// </summary>
public sealed class TransactionRegisteredConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RabbitMqConnectionProvider _connections;
    private readonly TransactionConsumerOptions _options;
    private readonly ILogger<TransactionRegisteredConsumer> _logger;

    public TransactionRegisteredConsumer(
        IServiceScopeFactory scopes,
        RabbitMqConnectionProvider connections,
        IOptions<TransactionConsumerOptions> options,
        ILogger<TransactionRegisteredConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _connections = connections;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Broker fora do ar na inicialização não pode matar o worker: ele
                // tenta de novo, do mesmo modo que o publisher do outro lado.
                _logger.LogError(exception, "Failed to attach the consumer; retrying");

                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var channel = await _connections.GetChannelAsync(stoppingToken);
        await channel.BasicQosAsync(0, _options.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(
            RabbitMqTopology.Queue, autoAck: false, consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var correlationId = delivery.BasicProperties.CorrelationId;

        // Liga cada log deste consumo à requisição HTTP de origem (ADR-011).
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["MessageId"] = delivery.BasicProperties.MessageId,
        });

        if (!TryRead(delivery, out var integrationEvent, out var reason))
        {
            _logger.LogError("Discarding unreadable message to the dead-letter queue: {Reason}", reason);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);

            return;
        }

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            try
            {
                await using var serviceScope = _scopes.CreateAsyncScope();
                await serviceScope.ServiceProvider
                    .GetRequiredService<ConsolidateTransactionUseCase>()
                    .Handle(integrationEvent!, stoppingToken);

                _logger.LogInformation(
                    "Consolidated transaction {TransactionId} into {Day}",
                    integrationEvent!.Data.TransactionId, integrationEvent.Data.OccurredAt.UtcDateTime.Date);

                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);

                return;
            }
            catch (DomainException exception)
            {
                // Erro permanente: DLQ direta, sem retry (ADR-003).
                _logger.LogError(exception, "Event violates a domain rule; sending to the dead-letter queue");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);

                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception, "Attempt {Attempt} of {MaxAttempts} failed", attempt, _options.MaxAttempts);

                if (attempt == _options.MaxAttempts)
                {
                    if (IsInfrastructureOutage(exception))
                    {
                        // A DLQ é para mensagem problemática, não para infraestrutura
                        // indisponível: devolvida à fila, a mensagem espera o banco
                        // voltar (ADR-003 §Revisão).
                        _logger.LogWarning(
                            exception, "Infrastructure is unavailable; returning the message to the queue");
                        await channel.BasicNackAsync(
                            delivery.DeliveryTag, multiple: false, requeue: true, stoppingToken);

                        return;
                    }

                    break;
                }

                // Espera real entre tentativas — requeue imediato criaria o laço
                // quente que a ADR-003 registra.
                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }

        _logger.LogError("Exhausted {MaxAttempts} attempts; sending to the dead-letter queue", _options.MaxAttempts);
        await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
    }

    /// <summary>
    /// `DbException.IsTransient` separa a falha de conectividade — que se resolve
    /// sozinha — da mensagem que nunca vai funcionar.
    /// </summary>
    private static bool IsInfrastructureOutage(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Envelope ilegível ou incompleto é erro permanente — sem retry.</summary>
    private static bool TryRead(
        BasicDeliverEventArgs delivery,
        out TransactionRegisteredEvent? integrationEvent,
        out string reason)
    {
        integrationEvent = null;

        try
        {
            integrationEvent = JsonSerializer.Deserialize<TransactionRegisteredEvent>(
                Encoding.UTF8.GetString(delivery.Body.Span), IntegrationEvents.SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            reason = exception.Message;

            return false;
        }

        if (integrationEvent is null || integrationEvent.EventId == Guid.Empty)
        {
            reason = "envelope is missing eventId";

            return false;
        }

        if (integrationEvent.Data is null || integrationEvent.Data.TransactionId == Guid.Empty)
        {
            reason = "envelope is missing data.transactionId";

            return false;
        }

        reason = string.Empty;

        return true;
    }
}
