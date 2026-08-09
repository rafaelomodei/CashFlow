using CashFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CashFlow.IntegrationTests.Api;

/// <summary>
/// A Cash Flow API real, sobre banco real, sem broker.
///
/// A ausência do broker é deliberada e é o ponto: se a API precisasse dele para
/// subir ou para responder `201`, estes testes não passariam — e RNF-001 estaria
/// quebrado sem ninguém perceber.
/// </summary>
public sealed class CashFlowApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
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
                ["ConnectionStrings:CashFlowDb"] = _database.GetConnectionString(),

                // Nenhum broker escuta aqui. O publisher vai falhar e retentar em
                // segundo plano, que é exatamente o comportamento sob RNF-001.
                ["RabbitMq:Host"] = "127.0.0.1",
                ["RabbitMq:Port"] = "1",
                ["RabbitMq:ConnectionTimeout"] = "00:00:02",
                ["OutboxPublisher:PollingInterval"] = "00:00:05",
            }));
    }

    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();
        await context.Database.ExecuteSqlRawAsync("TRUNCATE transactions, outbox_messages");
    }

    public async Task<int> PendingOutboxCountAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();

        return await context.OutboxMessages.AsNoTracking().CountAsync(message => message.ProcessedAt == null);
    }

    public async Task<string?> LastOutboxPayloadAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CashFlowDbContext>();

        return await context.OutboxMessages.AsNoTracking()
            .OrderByDescending(message => message.OccurredAt)
            .Select(message => message.Payload)
            .FirstOrDefaultAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

[CollectionDefinition(nameof(CashFlowApiCollection))]
public sealed class CashFlowApiCollection : ICollectionFixture<CashFlowApiFixture>;
