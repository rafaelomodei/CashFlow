using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Api;

/// <summary>
/// O `X-Correlation-Id` de `api-contracts.md` §1.5 e ADR-011.
/// </summary>
[Collection(nameof(CashFlowApiCollection))]
[Trait("Category", "Integration")]
public class CorrelationIdTests : IAsyncLifetime
{
    private readonly CashFlowApiFixture _fixture;
    private readonly HttpClient _client;

    public CorrelationIdTests(CashFlowApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WhenTheClientSendsOne_ItShouldComeBackUnchanged()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/transactions");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlationId);
    }

    [Fact]
    public async Task WhenTheClientSendsNone_TheServerShouldGenerateOne()
    {
        var response = await _client.GetAsync("/transactions");

        Guid.TryParse(response.Headers.GetValues("X-Correlation-Id").Single(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task ErrorResponses_ShouldCarryItInTheHeaderAndInTheBody()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, "/transactions")
        {
            Content = JsonContent.Create(new { type = "CREDIT", amount = -1m }),
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        // O corpo do erro é o que o cliente leva para o suporte; sem o
        // identificador ali, ele teria que copiar um header (§4.6).
        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlationId);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("correlationId").GetString().Should().Be(correlationId);
    }

    [Fact]
    public async Task ItShouldReachTheOutboxEnvelope()
    {
        var correlationId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, "/transactions")
        {
            Content = JsonContent.Create(new { type = "CREDIT", amount = 10.00m }),
        };
        request.Headers.Add("X-Correlation-Id", correlationId.ToString());

        await _client.SendAsync(request);

        // É aqui que a correlação deixa de ser um detalhe de HTTP e passa a
        // atravessar os quatro processos: ela viaja no envelope do evento.
        var payload = await _fixture.LastOutboxPayloadAsync();
        JsonDocument.Parse(payload!).RootElement.GetProperty("correlationId").GetString()
            .Should().Be(correlationId.ToString());
    }
}
