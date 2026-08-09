namespace CashFlow.Api.Http;

/// <summary>
/// O identificador de correlação da requisição em curso (§1.5, ADR-011).
///
/// Vive no `HttpContext`, e não em um serviço com estado: uma requisição não pode
/// enxergar o identificador de outra, e amarrá-lo ao contexto torna isso
/// impossível por construção.
/// </summary>
public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "CorrelationId";

    public static Guid Of(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Items.TryGetValue(ItemKey, out var value) && value is Guid correlationId
            ? correlationId
            : Guid.Empty;
    }

    internal static Guid Attach(HttpContext context)
    {
        // Um header ilegível não é motivo para recusar a requisição: ele existe
        // para diagnóstico, e gerar um novo preserva a rastreabilidade sem
        // transformar um detalhe de observabilidade em erro de negócio.
        var correlationId = Guid.TryParse(context.Request.Headers[HeaderName], out var provided)
            ? provided
            : Guid.NewGuid();

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString();

        return correlationId;
    }
}
