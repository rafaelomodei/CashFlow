using Consolidation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Consolidation.IntegrationTests.Persistence;

/// <summary>
/// PostgreSQL real para o <c>consolidation_db</c>, na mesma imagem do
/// <c>docker-compose.yml</c> (ADR-008, ADR-009). Container próprio, e não
/// compartilhado com o do contexto de lançamentos: os dois bancos são
/// independentes por decisão (ADR-005), e o teste não deveria ser o único lugar
/// onde eles se encontram.
/// </summary>
public sealed class ConsolidationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ConsolidationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE daily_balances, processed_events");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(ConsolidationDatabaseCollection))]
public sealed class ConsolidationDatabaseCollection : ICollectionFixture<ConsolidationDatabaseFixture>;
