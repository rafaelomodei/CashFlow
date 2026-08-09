using Consolidation.Api.Http;
using Consolidation.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    // Sem isto o escopo não é emitido, e o `correlationId` — que viaja em escopo
    // justamente para acompanhar todo log da requisição — nunca chega à saída.
    // A promessa de reconstruir a jornada de um lançamento com uma busca só
    // depende deste `true` (ADR-011).
    options.IncludeScopes = true;
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options => Consolidation.Api.Http.JsonOptions.Configure(options.JsonSerializerOptions));

builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

builder.Services.AddOpenApi();
builder.Services.AddConsolidationPersistence(builder.Configuration);
builder.Services.AddConsolidationHealthChecks();

var app = builder.Build();

await app.Services.ApplyConsolidationMigrationsAsync();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(StatusCodeHandler.WriteProblemAsync);

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Consolidation API"));
}

app.MapConsolidationHealthChecks();
app.MapControllers();

await app.RunAsync();

// Exposto para o WebApplicationFactory dos testes de integração.
public partial class Program;
