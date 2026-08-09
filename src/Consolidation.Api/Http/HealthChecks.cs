using Consolidation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Consolidation.Api.Http;

/// <summary>
/// Health checks de ADR-011 §3.
///
/// Aqui o banco **é** dependência obrigatória: sem o `consolidation_db` esta API
/// não tem o que responder, e dizer-se pronta seria mentir. É a diferença em
/// relação à Cash Flow API, cuja prontidão não considera o broker.
/// </summary>
public static class HealthChecks
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddConsolidationHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<ConsolidationDbContext>("consolidation-db", tags: [ReadyTag]);

        return services;
    }

    public static WebApplication MapConsolidationHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        });

        return app;
    }
}
