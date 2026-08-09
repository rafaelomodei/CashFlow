using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Http;

/// <summary>
/// Fábrica dos Problem Details de `api-contracts.md` §4.
///
/// Os `type` são identificadores estáveis, não URLs que precisem resolver, e o
/// `title` é a parte do corpo em que o cliente pode basear comportamento — por
/// isso os dois vivem aqui, em um lugar só, e não espalhados por controller.
/// </summary>
public static class Problems
{
    private const string Namespace = "https://cashflow.dev/problems/";

    public static ProblemDetails Validation(HttpContext context, IDictionary<string, string[]> errors)
    {
        var problem = Create(
            context, StatusCodes.Status400BadRequest, "validation-error", "Validation failed",
            "One or more fields are invalid.");

        problem.Extensions["errors"] = errors;

        return problem;
    }

    public static ProblemDetails Malformed(HttpContext context, string detail) =>
        Create(context, StatusCodes.Status400BadRequest, "malformed-request", "Malformed request", detail);

    public static ProblemDetails NotFound(HttpContext context, string detail) =>
        Create(context, StatusCodes.Status404NotFound, "not-found", "Resource not found", detail);

    public static ProblemDetails MethodNotAllowed(HttpContext context) =>
        Create(
            context, StatusCodes.Status405MethodNotAllowed, "method-not-allowed", "Method not allowed",
            "The HTTP method is not supported by this route.");

    public static ProblemDetails UnsupportedMediaType(HttpContext context) =>
        Create(
            context, StatusCodes.Status415UnsupportedMediaType, "unsupported-media-type", "Unsupported media type",
            "Content-Type must be 'application/json'.");

    /// <summary>
    /// Deliberadamente opaco: mensagem de exceção, nome de tabela e cadeia de
    /// conexão vão para o log, indexados pelo mesmo `correlationId` que o cliente
    /// recebe. Ele não ganha o diagnóstico, ganha a chave para obtê-lo (§4.6).
    /// </summary>
    public static ProblemDetails Internal(HttpContext context) =>
        Create(
            context, StatusCodes.Status500InternalServerError, "internal-error", "Internal server error",
            "An unexpected error occurred. Use the correlationId to trace it.");

    private static ProblemDetails Create(
        HttpContext context, int status, string type, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Type = Namespace + type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        };

        problem.Extensions["correlationId"] = CorrelationId.Of(context);

        return problem;
    }
}
