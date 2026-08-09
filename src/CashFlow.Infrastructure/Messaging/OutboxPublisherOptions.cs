namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Ritmo da varredura do outbox (ADR-004).
/// </summary>
public sealed class OutboxPublisherOptions
{
    public const string SectionName = "OutboxPublisher";

    /// <summary>
    /// Intervalo entre passagens quando tudo vai bem. Curto porque define a
    /// latência da consolidação, e a consistência é eventual mas não deveria ser
    /// lenta (ADR-006).
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Teto do backoff. Com o broker fora do ar por horas, o publisher tenta a
    /// cada meio minuto em vez de a cada dois segundos: o que estava pendente
    /// continua pendente, e o log não vira um carrossel.
    /// </summary>
    public TimeSpan MaxPollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int BatchSize { get; set; } = 100;
}
