namespace Consolidation.Api.Http;

/// <summary>
/// Aceita ou gera o `X-Correlation-Id`, devolve-o em **toda** resposta e o coloca
/// em escopo de log (§1.5, ADR-011).
///
/// O header é escrito antes de qualquer outro middleware produzir corpo: escrevê-lo
/// depois não teria efeito, porque a resposta já teria começado a sair.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = CorrelationId.Attach(context);

        // Em escopo, e não como argumento repetido em cada chamada: assim ele
        // acompanha inclusive os logs que o próprio framework emite.
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
        });

        await _next(context);
    }
}
