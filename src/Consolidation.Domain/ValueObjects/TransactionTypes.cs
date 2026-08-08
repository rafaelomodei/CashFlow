using Consolidation.Domain.Exceptions;

namespace Consolidation.Domain.ValueObjects;

/// <summary>
/// Fronteira de entrada do tipo: o evento carrega texto, e o saldo só é tocado
/// por um tipo já validado (RN-002).
/// </summary>
public static class TransactionTypes
{
    private const string CreditContractValue = "CREDIT";
    private const string DebitContractValue = "DEBIT";

    /// <summary>
    /// A comparação é sensível a maiúsculas de propósito: o produtor do evento
    /// emite exatamente <c>CREDIT</c> ou <c>DEBIT</c>, e aceitar outra grafia
    /// esconderia um produtor fora do contrato em vez de denunciá-lo.
    /// </summary>
    public static TransactionType Parse(string? value) => value switch
    {
        CreditContractValue => TransactionType.Credit,
        DebitContractValue => TransactionType.Debit,
        _ => throw new InvalidTransactionTypeException(),
    };
}
