// Composition root da Cash Flow API.
// Endpoints, validação e middleware entram na etapa 11 do roadmap.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

// Exposto para o WebApplicationFactory dos testes de integração (etapa 11).
public partial class Program;
