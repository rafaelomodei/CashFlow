using Consolidation.Application.Idempotency;
using Consolidation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consolidation.Infrastructure.Persistence;

/// <summary>
/// Sessão com o <c>consolidation_db</c> (ADR-005). É também a unidade de trabalho
/// que mantém juntos o efeito no saldo e a marcação do evento como processado —
/// separá-los invalidaria a idempotência (ADR-007).
/// </summary>
public sealed class ConsolidationDbContext : DbContext
{
    public ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsolidationDbContext).Assembly);
    }
}
