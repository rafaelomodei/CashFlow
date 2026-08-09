using Consolidation.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    // Sem isto o escopo não é emitido, e o `correlationId` — que viaja em escopo
    // justamente para acompanhar todo log da requisição — nunca chega à saída.
    // A promessa de reconstruir a jornada de um lançamento com uma busca só
    // depende deste `true` (ADR-011).
    options.IncludeScopes = true;
});

// O worker consome e persiste, mas não aplica migrations: quem cuida do esquema
// do `consolidation_db` é a API. Dois processos migrando o mesmo banco ao subir
// juntos é corrida sem ganho.
builder.Services.AddConsolidationPersistence(builder.Configuration);
builder.Services.AddConsolidationConsumer(builder.Configuration);

var host = builder.Build();

await host.RunAsync();
