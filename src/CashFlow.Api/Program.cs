using CashFlow.Api.Http;
using CashFlow.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Saída estruturada em JSON, sem dependência externa (ADR-011).
builder.Logging.AddJsonConsole();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => CashFlow.Api.Http.JsonOptions.Configure(options.JsonSerializerOptions));

// A validação automática do MVC produz um corpo próprio; o contrato define o
// dele (§4.2). Suprimir o filtro deixa uma única forma de erro na API.
builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

builder.Services.AddOpenApi();
builder.Services.AddCashFlowInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.ApplyCashFlowMigrationsAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(StatusCodeHandler.WriteProblemAsync);

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Cash Flow API"));
}

app.MapControllers();

await app.RunAsync();

// Exposto para o WebApplicationFactory dos testes de integração.
public partial class Program;
