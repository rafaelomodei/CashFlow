using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Consolidation.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelas ferramentas de linha de comando do EF Core, para gerar
/// migrations sem subir a API nem o worker. A cadeia de conexão aqui não conecta
/// a lugar nenhum em tempo de execução: a geração da migration precisa saber o
/// provedor, não o servidor.
/// </summary>
public sealed class ConsolidationDbContextFactory : IDesignTimeDbContextFactory<ConsolidationDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5433;Database=consolidation_db;Username=consolidation;Password=consolidation";

    public ConsolidationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ConsolidationDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__ConsolidationDb")
                ?? DesignTimeConnectionString)
            .Options);
}
