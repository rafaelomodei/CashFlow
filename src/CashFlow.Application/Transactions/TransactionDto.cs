using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;

namespace CashFlow.Application.Transactions;

/// <summary>
/// Lançamento como o contrato o expõe (`api-contracts.md` §2.1). A entidade não
/// atravessa a fronteira da aplicação: o formato de saída pode mudar sem que o
/// domínio mude.
/// </summary>
public sealed record TransactionDto(
    Guid Id,
    string Type,
    decimal Amount,
    DateTimeOffset OccurredAt,
    string? Description,
    DateTimeOffset CreatedAt)
{
    public static TransactionDto From(Transaction transaction) =>
        new(
            transaction.Id,
            transaction.Type.ToContractValue(),
            transaction.Amount.Amount,
            transaction.OccurredAt,
            transaction.Description,
            transaction.CreatedAt);
}
