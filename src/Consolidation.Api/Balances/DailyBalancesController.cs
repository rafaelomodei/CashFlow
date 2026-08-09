using System.Globalization;
using Consolidation.Api.Http;
using Consolidation.Application.Balances;
using Microsoft.AspNetCore.Mvc;

namespace Consolidation.Api.Balances;

/// <summary>
/// Consolidation API (`api-contracts.md` §3).
///
/// Lê o `consolidation_db`, que é banco próprio: responde normalmente com **toda**
/// a Cash Flow API fora do ar (RF-006, ADR-002, ADR-005).
/// </summary>
[ApiController]
[Route("daily-balances")]
public sealed class DailyBalancesController : ControllerBase
{
    private readonly GetDailyBalanceUseCase _getDailyBalance;

    public DailyBalancesController(GetDailyBalanceUseCase getDailyBalance) =>
        _getDailyBalance = getDailyBalance;

    /// <summary>
    /// Saldo consolidado de um dia (RF-004, RF-005). Dia sem movimentação devolve
    /// `200` com saldo zerado e `updatedAt` nulo — nunca `404` (ADR-006).
    /// </summary>
    [HttpGet("{date}")]
    [ProducesResponseType<DailyBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(string date, CancellationToken cancellationToken)
    {
        // `TryParseExact` e não `TryParse`: o contrato define `YYYY-MM-DD`, e
        // aceitar outras formas faria o servidor adivinhar se `03/04` é março ou
        // abril — em domínio financeiro, adivinhar data coloca o saldo no dia
        // errado sem falhar.
        if (!DateOnly.TryParseExact(
                date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            var problem = Problems.Validation(HttpContext, new Dictionary<string, string[]>
            {
                ["date"] = ["Date must be an existing calendar date in the format YYYY-MM-DD."],
            });

            return new ObjectResult(problem)
            {
                StatusCode = problem.Status,
                ContentTypes = { "application/problem+json" },
            };
        }

        return Ok(await _getDailyBalance.Handle(parsed, cancellationToken));
    }
}
