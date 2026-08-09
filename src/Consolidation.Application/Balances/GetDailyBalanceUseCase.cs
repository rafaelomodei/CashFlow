using Consolidation.Application.Abstractions;
using Consolidation.Domain.Entities;

namespace Consolidation.Application.Balances;

/// <summary>
/// UC-02 — consultar o saldo consolidado de um dia (RF-004, RF-005, RF-006).
/// </summary>
public sealed class GetDailyBalanceUseCase
{
    private readonly IDailyBalanceRepository _balances;

    public GetDailyBalanceUseCase(IDailyBalanceRepository balances) => _balances = balances;

    /// <summary>
    /// Sempre devolve um saldo. Dia sem lançamentos vale zero — ele não deixa de
    /// existir, e responder ausência obrigaria todo cliente a traduzir "não
    /// encontrado" para "zero", movendo regra de negócio para fora do sistema
    /// (ADR-006).
    /// </summary>
    public async Task<DailyBalanceDto> Handle(DateOnly date, CancellationToken cancellationToken)
    {
        var balance = await _balances.GetAsync(date, cancellationToken);

        return DailyBalanceDto.From(balance ?? DailyBalance.Empty(date));
    }
}
