using CashFlow.Api.Http;
using CashFlow.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Transactions;

/// <summary>
/// Cash Flow API (`api-contracts.md` §2).
///
/// O controller não valida regra de negócio: valor, tipo e período já são
/// validados pelo domínio e pelos casos de uso, e a exceção que eles lançam vira
/// `400` no middleware. Repetir a regra aqui criaria duas verdades que divergem
/// na primeira mudança.
/// </summary>
[ApiController]
[Route("transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly RegisterTransactionUseCase _register;
    private readonly GetTransactionUseCase _get;
    private readonly ListTransactionsUseCase _list;

    public TransactionsController(
        RegisterTransactionUseCase register,
        GetTransactionUseCase get,
        ListTransactionsUseCase list)
    {
        _register = register;
        _get = get;
        _list = list;
    }

    /// <summary>
    /// Registra um lançamento (RF-001, RF-002). Responde `201` mesmo com o
    /// RabbitMQ fora do ar: o evento vai para o outbox, não para o broker
    /// (RNF-001, ADR-004).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterTransactionRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Problem(Problems.Malformed(HttpContext, "The request body is required."));
        }

        if (!request.TryValidate(out var occurredAt, out var errors))
        {
            return Problem(Problems.Validation(HttpContext, errors));
        }

        var created = await _register.Handle(
            new RegisterTransactionCommand(
                request.Amount!.Value, request.Type!, occurredAt, request.Description,
                CorrelationId.Of(HttpContext)),
            cancellationToken);

        return Created($"/transactions/{created.Id}", created);
    }

    /// <summary>
    /// Consulta um lançamento (UC-06). É o destino do header `Location` de §2.1,
    /// e o único ponto da API em que `404` significa "não encontrado".
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        // A rota não usa a restrição `:guid`. Com ela, um id malformado não casaria
        // a rota e viraria `404` — mas o contrato distingue os dois casos: `400`
        // para id fora do formato, `404` para id válido e inexistente (§2.2).
        if (!Guid.TryParse(id, out var transactionId))
        {
            return Problem(Problems.Validation(HttpContext, new Dictionary<string, string[]>
            {
                ["id"] = ["Id must be a valid UUID."],
            }));
        }

        var found = await _get.Handle(transactionId, cancellationToken);

        return found is null
            ? Problem(Problems.NotFound(HttpContext, $"Transaction '{transactionId}' was not found."))
            : Ok(found);
    }

    /// <summary>
    /// Lista lançamentos com paginação por cursor e filtro por período (RF-003,
    /// ADR-014). Período sem lançamentos devolve `200` com coleção vazia — nunca
    /// `404`.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<TransactionPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var from = ParseDate(startDate, nameof(startDate), errors);
        var to = ParseDate(endDate, nameof(endDate), errors);

        if (errors.Count > 0)
        {
            return Problem(Problems.Validation(HttpContext, errors));
        }

        var page = await _list.Handle(new ListTransactionsQuery(limit, cursor, from, to), cancellationToken);

        return Ok(page);
    }

    private static DateOnly? ParseDate(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            errors[field] = [$"{field} must be a calendar date in the format YYYY-MM-DD."];

            return null;
        }

        return parsed;
    }

    private IActionResult Problem(ProblemDetails problem) =>
        new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" },
        };
}
