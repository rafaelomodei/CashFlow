using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using CashFlow.Application.Transactions;
using CashFlow.Infrastructure.Messaging;
using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Infrastructure;

/// <summary>
/// Registro das implementações do contexto de lançamentos.
///
/// Vive na infraestrutura porque é ela que conhece as implementações; o
/// *composition root* apenas chama. É o que permite ao `Program.cs` continuar
/// legível e à camada de aplicação continuar sem saber quem a implementa
/// (ADR-001).
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "CashFlowDb";

    public static IServiceCollection AddCashFlowInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<CashFlowDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<RegisterTransactionUseCase>();
        services.AddScoped<GetTransactionUseCase>();
        services.AddScoped<ListTransactionsUseCase>();
        services.AddScoped<PublishPendingOutboxMessagesUseCase>();

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<OutboxPublisherOptions>(configuration.GetSection(OutboxPublisherOptions.SectionName));

        // Singleton: a conexão com o broker é cara e reaproveitável, e o provider
        // já reabre sozinha quando ela morre.
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddHostedService<OutboxPublisherService>();

        return services;
    }

    /// <summary>
    /// Aplica as migrations na inicialização, para que um clone limpo suba
    /// funcional com um comando só. Em produção o passo pertence ao deploy —
    /// registrado como melhoria futura no README.
    /// </summary>
    public static async Task ApplyCashFlowMigrationsAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CashFlowDbContext>().Database.MigrateAsync();
    }
}
