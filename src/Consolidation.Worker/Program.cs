// Composition root do Consolidation Worker.
// O consumidor de eventos entra na etapa 10 do roadmap.

var builder = Host.CreateApplicationBuilder(args);

var host = builder.Build();

host.Run();
