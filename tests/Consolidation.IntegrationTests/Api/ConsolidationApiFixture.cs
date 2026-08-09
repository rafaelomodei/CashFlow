using Consolidation.Application.Balances;
using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using Consolidation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Consolidation.IntegrationTests.Api;

/// <summary>
/// A Consolidation API real, sobre banco real, sem broker e sem a Cash Flow API.
///
/// A ausência das duas é o ponto: RF-006 exige que a consulta ao saldo funcione
/// com o serviço de lançamentos fora do ar, e a única forma honesta de verificar
/// isso é não ter o serviço de lançamentos.
/// </summary>
public sealed class ConsolidationApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConsolidationDb"] = _database.GetConnectionString(),
            }));
    }

    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE daily_balances, processed_events");
    }

    /// <summary>Grava um saldo direto, sem passar pelo consumidor.</summary>
    public async Task SeedAsync(DateOnly date, decimal credits, decimal debits)
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

        var balance = DailyBalance.Empty(date);
        if (credits > 0)
        {
            balance.Apply(TransactionType.Credit, Money.Create(credits));
        }

        if (debits > 0)
        {
            balance.Apply(TransactionType.Debit, Money.Create(debits));
        }

        context.DailyBalances.Add(balance);
        await context.SaveChangesAsync();
    }

    public async Task ConsolidateAsync(Shared.Contracts.TransactionRegisteredEvent integrationEvent)
    {
        await using var scope = Services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ConsolidateTransactionUseCase>()
            .Handle(integrationEvent, CancellationToken.None);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ConsolidationApiCollection))]
public sealed class ConsolidationApiCollection : ICollectionFixture<ConsolidationApiFixture>;
