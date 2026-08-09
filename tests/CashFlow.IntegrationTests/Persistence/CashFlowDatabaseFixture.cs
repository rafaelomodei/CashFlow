using CashFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CashFlow.IntegrationTests.Persistence;

/// <summary>
/// PostgreSQL real, na mesma imagem do <c>docker-compose.yml</c> (ADR-008,
/// ADR-009). Um banco em memória responderia a estes testes sem exercitar o que
/// eles existem para verificar: <c>numeric(18,2)</c>, <c>jsonb</c>, comparação de
/// tupla e o comportamento transacional.
/// </summary>
public sealed class CashFlowDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Migrate, e não EnsureCreated: o esquema sob teste passa a ser o mesmo
        // que será aplicado em produção — uma migration divergente do modelo
        // falha aqui, e não no deploy.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public CashFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE transactions, outbox_messages");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>
/// Um único container para todas as classes de teste de persistência: subir um
/// PostgreSQL por classe multiplicaria o custo do gate de integração sem
/// aumentar o isolamento, que <see cref="CashFlowDatabaseFixture.ResetAsync"/>
/// já garante.
/// </summary>
[CollectionDefinition(nameof(CashFlowDatabaseCollection))]
public sealed class CashFlowDatabaseCollection : ICollectionFixture<CashFlowDatabaseFixture>;
