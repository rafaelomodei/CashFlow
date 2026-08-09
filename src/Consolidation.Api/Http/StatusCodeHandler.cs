using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Consolidation.Api.Http;

/// <summary>
/// Dá corpo de Problem Details às respostas que o pipeline produz sem passar por
/// controller — rota inexistente, verbo não suportado, `Content-Type` recusado
/// (§4.4, §4.5, §4.7).
///
/// Sem isso, essas respostas sairiam com corpo vazio, e o cliente teria dois
/// formatos de erro para tratar em vez de um.
/// </summary>
public static class StatusCodeHandler
{
    public static async Task WriteProblemAsync(StatusCodeContext statusCodeContext)
    {
        ArgumentNullException.ThrowIfNull(statusCodeContext);

        var context = statusCodeContext.HttpContext;
        var problem = context.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => Problems.NotFound(context, "The requested route does not exist."),
            StatusCodes.Status405MethodNotAllowed => Problems.MethodNotAllowed(context),
            StatusCodes.Status415UnsupportedMediaType => Problems.UnsupportedMediaType(context),
            StatusCodes.Status400BadRequest => Problems.Malformed(context, "The request could not be understood."),
            _ => null,
        };

        if (problem is null)
        {
            return;
        }

        context.Response.ContentType = "application/problem+json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, Consolidation.Api.Http.JsonOptions.Default));
    }
}
