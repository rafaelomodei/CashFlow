using Consolidation.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonConsole();

// O worker consome e persiste, mas não aplica migrations: quem cuida do esquema
// do `consolidation_db` é a API. Dois processos migrando o mesmo banco ao subir
// juntos é corrida sem ganho.
builder.Services.AddConsolidationPersistence(builder.Configuration);
builder.Services.AddConsolidationConsumer(builder.Configuration);

var host = builder.Build();

await host.RunAsync();
