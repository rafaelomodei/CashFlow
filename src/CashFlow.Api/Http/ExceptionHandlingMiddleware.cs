using System.Text.Json;
using CashFlow.Application.Exceptions;
using CashFlow.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CashFlow.Api.Http;

/// <summary>
/// Traduz exceção em Problem Details (§4).
///
/// A borda HTTP distingue **violação de regra** de **falha do servidor** sem
/// conhecer cada regra: é para isso que existe uma raiz `DomainException` por
/// contexto. Um `catch` por tipo concreto precisaria ser editado a cada regra
/// nova, e esqueceria de ser.
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
                // Sem como trocar o status: o cliente já recebeu os headers.
                // Registrar é tudo o que resta, e é melhor que mascarar.
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
                context, new Dictionary<string, string[]> { [FieldOf(domain)] = [domain.Message] }),
            InvalidQueryException query => Problems.Validation(
                context, new Dictionary<string, string[]> { ["query"] = [query.Message] }),
            BadHttpRequestException => Problems.Malformed(context, "The request body is not valid JSON."),
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

        // O header de correlação some no `Clear`; recolocá-lo é o que faz a
        // promessa de §1.5 valer também para as respostas de erro.
        context.Response.Headers[CorrelationId.HeaderName] = CorrelationId.Of(context).ToString();

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, CashFlow.Api.Http.JsonOptions.Default));
    }

    /// <summary>
    /// Mapeia a exceção de domínio para o campo do contrato que a originou, de
    /// modo que o cliente saiba onde corrigir.
    /// </summary>
    private static string FieldOf(DomainException exception) => exception switch
    {
        InvalidAmountException => "amount",
        InvalidTransactionTypeException => "type",
        InvalidOccurrenceDateException => "occurredAt",
        InvalidDescriptionException => "description",
        _ => "request",
    };
}
