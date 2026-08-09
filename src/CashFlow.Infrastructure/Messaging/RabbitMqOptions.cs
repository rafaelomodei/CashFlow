namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Endereço e credenciais do broker. Só o que muda entre ambientes: a forma da
/// topologia é contrato e vive em <see cref="RabbitMqTopology"/>.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>
    /// Curto de propósito: quem espera é um serviço em segundo plano que vai
    /// tentar de novo, e uma tentativa que demora um minuto para falhar só atrasa
    /// a seguinte.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
