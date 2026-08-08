using CashFlow.Domain.Exceptions;

namespace CashFlow.Domain.ValueObjects;

/// <summary>
/// Comportamento de <see cref="TransactionType"/>. A regra de sinal (RN-003) e a
/// tradução para o vocabulário do contrato existem aqui uma única vez: enquanto
/// forem `if (type == "DEBIT")` espalhados, elas divergem (ADR-013).
/// </summary>
public static class TransactionTypes
{
    private const string CreditContractValue = "CREDIT";
    private const string DebitContractValue = "DEBIT";

    /// <summary>
    /// Efeito do lançamento sobre o saldo: o sinal deriva do tipo, nunca do
    /// valor (RN-003).
    /// </summary>
    public static decimal ApplyTo(this TransactionType type, Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return type switch
        {
            TransactionType.Credit => amount.Amount,
            TransactionType.Debit => -amount.Amount,
            _ => throw new InvalidTransactionTypeException(),
        };
    }

    public static string ToContractValue(this TransactionType type) => type switch
    {
        TransactionType.Credit => CreditContractValue,
        TransactionType.Debit => DebitContractValue,
        _ => throw new InvalidTransactionTypeException(),
    };

    /// <summary>
    /// Converte o valor do contrato no tipo do domínio. A comparação é sensível
    /// a maiúsculas de propósito: aceitar `credit` faria o sistema receber uma
    /// grafia e devolver outra.
    /// </summary>
    public static TransactionType Parse(string? value) => value switch
    {
        CreditContractValue => TransactionType.Credit,
        DebitContractValue => TransactionType.Debit,
        _ => throw new InvalidTransactionTypeException(),
    };
}
