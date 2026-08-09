using Consolidation.Domain.Entities;

namespace Consolidation.Application.Balances;

/// <summary>
/// Saldo do dia como o contrato o expõe (`api-contracts.md` §3.1).
/// </summary>
/// <param name="UpdatedAt">
/// Nulo enquanto o dia nunca foi consolidado. Não é metadado decorativo: é a
/// defasagem observável da consolidação, e sem ele o cliente não distingue
/// "saldo atualizado" de "worker parado há duas horas" (ADR-006).
/// </param>
public sealed record DailyBalanceDto(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Balance,
    DateTimeOffset? UpdatedAt)
{
    public static DailyBalanceDto From(DailyBalance balance) =>
        new(
            balance.Date,
            balance.TotalCredits.Amount,
            balance.TotalDebits.Amount,
            balance.Balance.Amount,
            balance.UpdatedAt);
}
