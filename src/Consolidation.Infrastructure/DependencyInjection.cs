using Consolidation.Application.Abstractions;
using Consolidation.Application.Balances;
using Consolidation.Infrastructure.Messaging;
using Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Consolidation.Infrastructure;

/// <summary>
/// Registro das implementações do contexto de consolidação.
///
/// A API e o worker registram partes diferentes: a API só lê, e não precisa do
/// consumidor; o worker consome, e não expõe HTTP. Registrar tudo dos dois lados
/// faria a API abrir conexão com o broker sem ter o que fazer com ela.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "ConsolidationDb";

    /// <summary>Persistência e leitura — o que a API precisa.</summary>
    public static IServiceCollection AddConsolidationPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ConsolidationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        services.AddScoped<IDailyBalanceRepository, DailyBalanceRepository>();
        services.AddScoped<IProcessedEventRepository, ProcessedEventRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<GetDailyBalanceUseCase>();
        services.AddScoped<ConsolidateTransactionUseCase>();

        return services;
    }

    /// <summary>Consumo de eventos — o que só o worker precisa.</summary>
    public static IServiceCollection AddConsolidationConsumer(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<TransactionConsumerOptions>(
            configuration.GetSection(TransactionConsumerOptions.SectionName));

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddHostedService<TransactionRegisteredConsumer>();

        return services;
    }

    /// <summary>
    /// Só a API aplica as migrations do `consolidation_db`. Dois processos
    /// migrando o mesmo banco ao subir juntos é corrida sem ganho.
    /// </summary>
    public static async Task ApplyConsolidationMigrationsAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>().Database.MigrateAsync();
    }
}
