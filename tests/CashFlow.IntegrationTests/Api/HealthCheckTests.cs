using System.Net;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Api;

/// <summary>
/// Health checks da Cash Flow API (ADR-011 §3).
/// </summary>
[Collection(nameof(CashFlowApiCollection))]
[Trait("Category", "Integration")]
public class HealthCheckTests
{
    private readonly HttpClient _client;

    public HealthCheckTests(CashFlowApiFixture fixture) => _client = fixture.CreateClient();

    [Fact]
    public async Task Live_ShouldAnswerHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ShouldNotDependOnTheBroker()
    {
        // Nenhum broker escuta na porta que esta fixture configura, e mesmo assim
        // a API está pronta. Se o `ready` considerasse o RabbitMQ, um orquestrador
        // retiraria a API de serviço quando o broker caísse — a instrumentação
        // produzindo a indisponibilidade que RNF-001 pede para evitar.
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
