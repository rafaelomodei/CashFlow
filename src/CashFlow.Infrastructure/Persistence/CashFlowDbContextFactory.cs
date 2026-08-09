using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Usado apenas pelas ferramentas de linha de comando do EF Core, para gerar
/// migrations sem subir a API. A alternativa seria a ferramenta construir o host
/// da aplicação inteiro — que ainda nem registra este contexto, e cuja
/// composição pertence à etapa 11.
///
/// A cadeia de conexão aqui não conecta a lugar nenhum em tempo de execução: o
/// que a geração da migration precisa saber é o provedor, não o servidor.
/// </summary>
public sealed class CashFlowDbContextFactory : IDesignTimeDbContextFactory<CashFlowDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=cashflow_db;Username=cashflow;Password=cashflow";

    public CashFlowDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CashFlowDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__CashFlowDb")
                ?? DesignTimeConnectionString)
            .Options);
}
