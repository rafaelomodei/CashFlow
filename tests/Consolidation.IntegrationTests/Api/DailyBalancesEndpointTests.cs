using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Consolidation.IntegrationTests.Api;

/// <summary>
/// `GET /daily-balances/{date}` contra o contrato de `api-contracts.md` §3.
/// </summary>
[Collection(nameof(ConsolidationApiCollection))]
[Trait("Category", "Integration")]
public class DailyBalancesEndpointTests : IAsyncLifetime
{
    private readonly ConsolidationApiFixture _fixture;
    private readonly HttpClient _client;

    public DailyBalancesEndpointTests(ConsolidationApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_ShouldReturnTheConsolidatedBalanceOfTheDay()
    {
        await _fixture.SeedAsync(new DateOnly(2026, 8, 8), credits: 1500.00m, debits: 700.00m);

        var response = await _client.GetAsync("/daily-balances/2026-08-08");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Json(response);
        body.GetProperty("date").GetString().Should().Be("2026-08-08");
        body.GetProperty("totalCredits").GetDecimal().Should().Be(1500.00m);
        body.GetProperty("totalDebits").GetDecimal().Should().Be(700.00m);
        body.GetProperty("balance").GetDecimal().Should().Be(800.00m);
        body.GetProperty("updatedAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task Get_ForADayWithoutMovement_ShouldReturnZeroesAndNullUpdatedAt()
    {
        var response = await _client.GetAsync("/daily-balances/2026-08-09");

        // Nunca `404`: um dia sem movimentação tem saldo zero, e zero é um valor.
        // `404` obrigaria todo cliente a traduzir "não encontrado" para "zero",
        // movendo regra de negócio para fora do sistema (ADR-006).
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Json(response);
        body.GetProperty("balance").GetDecimal().Should().Be(0.00m);
        body.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Get_ForAFutureDate_ShouldAnswerTheSameWayAsAnEmptyDay()
    {
        var response = await _client.GetAsync("/daily-balances/2099-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(response)).GetProperty("balance").GetDecimal().Should().Be(0.00m);
    }

    [Fact]
    public async Task Get_ShouldAllowANegativeBalance()
    {
        await _fixture.SeedAsync(new DateOnly(2026, 8, 10), credits: 100.00m, debits: 250.50m);

        var body = await Json(await _client.GetAsync("/daily-balances/2026-08-10"));

        body.GetProperty("balance").GetDecimal().Should().Be(-150.50m);
        body.GetProperty("totalDebits").GetDecimal().Should().Be(250.50m, "o débito não trafega com sinal");
    }

    [Theory]
    [InlineData("/daily-balances/08-08-2026")]
    [InlineData("/daily-balances/2026-8-8")]
    [InlineData("/daily-balances/2026-02-30")]
    [InlineData("/daily-balances/ontem")]
    public async Task Get_WithAnInvalidDate_ShouldReturnValidationProblem(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await Json(response);
        body.GetProperty("type").GetString().Should().Be("https://cashflow.dev/problems/validation-error");
        body.GetProperty("errors").TryGetProperty("date", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Get_ShouldEchoTheCorrelationId()
    {
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/daily-balances/2026-08-08");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlationId);
    }

    [Fact]
    public async Task Get_ShouldAnswerWithoutTheCashFlowServiceAndWithoutTheBroker()
    {
        await _fixture.SeedAsync(new DateOnly(2026, 8, 11), credits: 42.00m, debits: 0m);

        var response = await _client.GetAsync("/daily-balances/2026-08-11");

        // RF-006. Não há o que derrubar neste teste porque não há o que subir: a
        // fixture não tem `cashflow_db`, não tem broker e não carrega assembly
        // algum do outro contexto. A independência não é verificada por um
        // teardown — ela é a própria montagem (ADR-002, ADR-005).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Json(response)).GetProperty("balance").GetDecimal().Should().Be(42.00m);
    }

    [Fact]
    public async Task OpenApi_ShouldDescribeTheRouteTheContractDefines()
    {
        var document = await Json(await _client.GetAsync("/openapi/v1.json"));

        var paths = document.GetProperty("paths");
        paths.TryGetProperty("/daily-balances/{date}", out var route).Should().BeTrue(
            "a especificação gerada precisa concordar com api-contracts.md §3");
        route.TryGetProperty("get", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UnknownRoute_ShouldReturnNotFoundAsProblemDetails()
    {
        var response = await _client.GetAsync("/rota-que-nao-existe");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Json(response)).GetProperty("type").GetString()
            .Should().Be("https://cashflow.dev/problems/not-found");
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}
