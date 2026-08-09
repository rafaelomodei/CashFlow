using System.Text.Json;
using Consolidation.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Consolidation.Api.Http;

/// <summary>
/// Traduz exceção em Problem Details (§4).
///
/// É uma cópia da que existe na Cash Flow API, e não código compartilhado: os
/// dois serviços não se referenciam (ADR-002), e uma biblioteca comum de HTTP
/// faria uma mudança em um lado exigir redeploy do outro — o acoplamento que a
/// decomposição existe para evitar.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Failure after the response had already started");

                throw;
            }

            await WriteAsync(context, exception);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception exception)
    {
        var problem = exception switch
        {
            DomainException domain => Problems.Validation(
                context, new Dictionary<string, string[]> { ["request"] = [domain.Message] }),
            BadHttpRequestException => Problems.Malformed(context, "The request could not be understood."),
            _ => Problems.Internal(context),
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled failure while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Request rejected: {Detail}", exception.Message);
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.Headers[CorrelationId.HeaderName] = CorrelationId.Of(context).ToString();

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions.Default));
    }
}
