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
/// (RF-004, ADR-003, ADR-007).
///
/// O `ack` é manual e só acontece **depois** do commit no banco: reconhecer antes
/// tiraria a mensagem da fila sem o efeito persistido, e ela não voltaria.
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

        // O correlationId em escopo alcança todo log deste consumo, inclusive os
        // que não passam por aqui — é o que liga esta linha à requisição HTTP que
        // originou o lançamento, quatro processos atrás (ADR-011).
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
                // Violação de regra não melhora com o tempo: um valor não positivo
                // continuará não positivo na décima tentativa. Vai direto para a
                // DLQ, como a mensagem ilegível.
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
                        // A DLQ é para mensagem problemática, não para
                        // indisponibilidade de infraestrutura. Um banco fora do ar
                        // por mais tempo que a janela de retry mandaria para lá
                        // eventos perfeitamente válidos, e a recuperação deixaria
                        // de ser automática — contrariando a promessa de perda
                        // zero (RNF-004, RNF-007). Devolvida à fila, a mensagem
                        // espera o banco voltar.
                        _logger.LogWarning(
                            exception, "Infrastructure is unavailable; returning the message to the queue");
                        await channel.BasicNackAsync(
                            delivery.DeliveryTag, multiple: false, requeue: true, stoppingToken);

                        return;
                    }

                    break;
                }

                // Espera real entre tentativas. `nack` com `requeue=true` devolveria
                // a mensagem à frente da fila e ela voltaria de imediato — o laço
                // quente que a ADR-003 registra como o erro comum aqui.
                await Task.Delay(_options.RetryDelay, stoppingToken);
            }
        }

        _logger.LogError("Exhausted {MaxAttempts} attempts; sending to the dead-letter queue", _options.MaxAttempts);
        await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
    }

    /// <summary>
    /// Distingue "esta mensagem é ruim" de "o mundo em volta está fora do ar".
    ///
    /// `DbException.IsTransient` é o próprio provedor dizendo que a falha é de
    /// conectividade e não da instrução — é a informação que separa um evento
    /// que nunca vai funcionar de um que vai funcionar assim que o banco voltar.
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

    /// <summary>
    /// Mensagem ilegível ou incompleta é erro permanente, e por isso é separada
    /// do retry: o envelope não fica válido na segunda leitura.
    /// </summary>
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
