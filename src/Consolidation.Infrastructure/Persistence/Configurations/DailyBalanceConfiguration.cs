using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Consolidation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <c>daily_balances</c> (architecture.md §9).
/// </summary>
public sealed class DailyBalanceConfiguration : IEntityTypeConfiguration<DailyBalance>
{
    public void Configure(EntityTypeBuilder<DailyBalance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("daily_balances");

        // A data é a chave: um dia tem um saldo, não vários. É ela que permite ao
        // consumidor atualizar a linha existente em vez de acumular linhas.
        builder.HasKey(balance => balance.Date);

        builder.Property(balance => balance.Date)
            .HasColumnName("date")
            .HasColumnType("date");

        builder.Property(balance => balance.TotalCredits)
            .HasColumnName("total_credits")
            .HasColumnType("numeric(18,2)")
            .HasConversion(total => total.Amount, value => Money.Create(value))
            .IsRequired();

        builder.Property(balance => balance.TotalDebits)
            .HasColumnName("total_debits")
            .HasColumnType("numeric(18,2)")
            .HasConversion(total => total.Amount, value => Money.Create(value))
            .IsRequired();

        // `UpdatedAt` é nulável na entidade porque DailyBalance.Empty representa
        // um dia nunca consolidado — e esse dia não existe como linha. Toda linha
        // gravada passou por Apply, então a coluna é NOT NULL: o banco recusa um
        // saldo que ninguém consolidou em vez de guardá-lo.
        builder.Property(balance => balance.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Saldo é derivado dos dois totais, e não persistido: uma terceira coluna
        // seria uma terceira coisa para divergir das duas que a originaram.
        builder.Ignore(balance => balance.Balance);
    }
}
