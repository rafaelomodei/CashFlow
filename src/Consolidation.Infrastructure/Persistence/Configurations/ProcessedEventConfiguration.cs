using Consolidation.Application.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Consolidation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <c>processed_events</c> (ADR-007, architecture.md §9).
/// </summary>
public sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("processed_events");

        // A tabela inteira existe por causa desta chave: ela é o que transforma
        // reprocessamento em violação de unicidade detectável, em vez de uma soma
        // duplicada e silenciosa. Uma consulta prévia não substitui isso — dois
        // consumidores concorrentes passariam os dois por ela.
        builder.HasKey(processedEvent => processedEvent.EventId);

        builder.Property(processedEvent => processedEvent.EventId)
            .HasColumnName("event_id");

        builder.Property(processedEvent => processedEvent.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
