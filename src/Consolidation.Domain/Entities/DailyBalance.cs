using Consolidation.Domain.Exceptions;
using Consolidation.Domain.ValueObjects;

namespace Consolidation.Domain.Entities;

/// <summary>
/// Saldo consolidado de um dia (RF-004). O saldo não é armazenado: ele é a
/// diferença entre os dois totais, e por isso não pode divergir deles.
/// </summary>
public sealed class DailyBalance
{
    private DailyBalance(DateOnly date, Money totalCredits, Money totalDebits, DateTimeOffset? updatedAt)
    {
        Date = date;
        TotalCredits = totalCredits;
        TotalDebits = totalDebits;
        UpdatedAt = updatedAt;
    }

    public DateOnly Date { get; }

    public Money TotalCredits { get; private set; }

    /// <summary>Sempre positivo: o sinal do débito vive em <see cref="Balance"/>.</summary>
    public Money TotalDebits { get; private set; }

    public Money Balance => TotalCredits.Subtract(TotalDebits);

    /// <summary>
    /// Instante da última aplicação de evento. Nulo enquanto o dia nunca foi
    /// consolidado — é a evidência de consistência eventual que o contrato expõe
    /// ao cliente (ADR-006).
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    /// <summary>
    /// Dia ao qual um lançamento pertence: a data civil em UTC do instante em que
    /// ele ocorreu (RN-004, premissa P-04).
    /// </summary>
    public static DateOnly DayOf(DateTimeOffset occurredAt) =>
        DateOnly.FromDateTime(occurredAt.UtcDateTime);

    /// <summary>
    /// Dia sem nenhum lançamento consolidado. Não é ausência de saldo, é saldo
    /// zero — a razão de a consulta responder 200 e nunca 404 (ADR-006).
    /// </summary>
    public static DailyBalance Empty(DateOnly date) =>
        new(date, Money.Zero, Money.Zero, updatedAt: null);

    public void Apply(TransactionType type, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!Enum.IsDefined(type))
        {
            throw new InvalidTransactionTypeException();
        }

        // O evento sempre carrega valor positivo: o sinal é do tipo, nunca do
        // valor (RN-003). Um valor negativo aqui corromperia o total em silêncio.
        if (!amount.IsPositive)
        {
            throw new InvalidAmountException("Amount must be greater than zero.");
        }

        switch (type)
        {
            case TransactionType.Credit:
                TotalCredits = TotalCredits.Add(amount);
                break;
            case TransactionType.Debit:
                TotalDebits = TotalDebits.Add(amount);
                break;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
