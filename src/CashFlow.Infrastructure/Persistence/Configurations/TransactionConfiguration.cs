using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <c>transactions</c> (architecture.md §9). O domínio não carrega
/// atributo de persistência algum: a tradução para colunas vive aqui, do lado de
/// fora (ADR-001).
/// </summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("transactions");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id");

        // A leitura passa por Money.Create, e não por um construtor cru: um valor
        // que viole RN-001 no banco falha ao ser carregado em vez de circular
        // como se fosse válido.
        builder.Property(transaction => transaction.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .HasConversion(
                amount => amount.Amount,
                value => Money.Create(value))
            .IsRequired();

        // Gravado pelo nome do contrato, nunca pelo ordinal do enum: reordenar
        // TransactionType não pode reinterpretar linha já gravada (ADR-013).
        builder.Property(transaction => transaction.Type)
            .HasColumnName("type")
            .HasMaxLength(6)
            .HasConversion(
                type => type.ToContractValue(),
                value => TransactionTypes.Parse(value))
            .IsRequired();

        builder.Property(transaction => transaction.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .HasColumnName("description")
            .HasMaxLength(200);

        builder.Property(transaction => transaction.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Índice da paginação por cursor: ele é o que torna a busca da posição um
        // seek, e não uma varredura que cresce com a profundidade do scroll
        // (ADR-014).
        builder.HasIndex(transaction => new { transaction.OccurredAt, transaction.Id })
            .HasDatabaseName("ix_transactions_occurred_at_id")
            .IsDescending(true, true);
    }
}
