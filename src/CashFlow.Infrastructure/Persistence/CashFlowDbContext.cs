using CashFlow.Application.Outbox;
using CashFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Sessão com o <c>cashflow_db</c> (ADR-005). É também a unidade de trabalho que
/// torna o lançamento e o evento do outbox atômicos: os dois nascem no mesmo
/// rastreamento e vão ao banco no mesmo <c>SaveChanges</c> (ADR-004).
/// </summary>
public sealed class CashFlowDbContext : DbContext
{
    public CashFlowDbContext(DbContextOptions<CashFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CashFlowDbContext).Assembly);
    }
}
