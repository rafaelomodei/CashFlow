using CashFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Api.Http;

/// <summary>
/// Health checks de ADR-011 §3.
///
/// O ponto arquitetural desta API está no que o `ready` **não** verifica: o
/// RabbitMQ. Com o broker fora do ar o registro de lançamentos continua correto,
/// porque o evento vai para o outbox (ADR-004). Marcar a API como não-pronta faria
/// um orquestrador retirá-la de serviço — a própria instrumentação passaria a
/// produzir a indisponibilidade que RNF-001 pede para evitar.
/// </summary>
public static class HealthChecks
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddCashFlowHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<CashFlowDbContext>("cashflow-db", tags: [ReadyTag]);

        return services;
    }

    public static WebApplication MapCashFlowHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // `live` não checa dependência alguma: ele responde à pergunta "o processo
        // está de pé?", e reiniciar um processo saudável porque o banco caiu só
        // troca uma indisponibilidade por duas.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        });

        return app;
    }
}
