namespace Consolidation.Infrastructure.Messaging;

/// <summary>
/// Endereço e credenciais do broker para o lado consumidor.
///
/// É uma classe própria, e não a do contexto de lançamentos: os dois serviços são
/// independentes por decisão (ADR-002), e compartilhar configuração faria uma
/// mudança em um lado exigir redeploy do outro. O que **não** pode divergir são os
/// nomes da topologia, e por isso eles vivem em <c>Shared.Contracts</c>.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
