using CashFlow.Application.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <c>outbox_messages</c> (ADR-004, architecture.md §9).
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        // A chave primária é o eventId do envelope: a mesma emissão não pode
        // virar duas mensagens (ADR-004 §Esquema).
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id");

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(100)
            .IsRequired();

        // jsonb, e não text: o payload é auditável por consulta quando for
        // preciso descobrir o que foi emitido (ADR-005).
        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(message => message.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamptz");

        builder.Property(message => message.Attempts)
            .HasColumnName("attempts")
            .IsRequired();

        builder.Property(message => message.Error)
            .HasColumnName("error")
            .HasColumnType("text");

        // Índice parcial: a varredura de pendentes precisa continuar barata
        // enquanto a tabela cresce com mensagens já publicadas, que serão a
        // esmagadora maioria das linhas (ADR-004).
        builder.HasIndex(message => message.OccurredAt)
            .HasDatabaseName("ix_outbox_messages_pending")
            .HasFilter("processed_at IS NULL");
    }
}
