using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace CashFlow.IntegrationTests.Api;

/// <summary>
/// `POST /transactions`, `GET /transactions/{id}` e `GET /transactions` contra o
/// contrato de `api-contracts.md` §2 e §4.
/// </summary>
[Collection(nameof(CashFlowApiCollection))]
[Trait("Category", "Integration")]
public class TransactionsEndpointsTests : IAsyncLifetime
{
    private readonly CashFlowApiFixture _fixture;
    private readonly HttpClient _client;

    public TransactionsEndpointsTests(CashFlowApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Post_ShouldCreateTheTransactionAndPointLocationAtIt()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type = "CREDIT",
            amount = 1500.00m,
            occurredAt = "2026-08-08T14:30:00Z",
            description = "Venda no balcão",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await Json(response);
        var id = body.GetProperty("id").GetString();

        response.Headers.Location!.ToString().Should().Be($"/transactions/{id}");
        body.GetProperty("type").GetString().Should().Be("CREDIT");
        body.GetProperty("amount").GetDecimal().Should().Be(1500.00m);
        body.GetProperty("occurredAt").GetString().Should().Be("2026-08-08T14:30:00Z");
        body.GetProperty("description").GetString().Should().Be("Venda no balcão");
        body.GetProperty("createdAt").GetString().Should().EndWith("Z");

        // O recurso criado é buscável no destino que o `Location` aponta.
        var fetched = await _client.GetAsync(response.Headers.Location);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WithTheBrokerDown_ShouldStillReturnCreated()
    {
        // O requisito que define a arquitetura inteira: nenhum broker escuta na
        // porta configurada nesta fixture, e mesmo assim o lançamento é registrado
        // e o evento fica retido no outbox (RNF-001, ADR-004).
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type = "DEBIT",
            amount = 90.50m,
            occurredAt = "2026-08-08T10:00:00Z",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await _fixture.PendingOutboxCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Post_WithoutOccurredAt_ShouldUseTheServerInstant()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new { type = "CREDIT", amount = 10.00m });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await Json(response);
        body.GetProperty("occurredAt").GetString().Should().Be(body.GetProperty("createdAt").GetString());
    }

    [Theory]
    [InlineData("CREDIT", 0, "amount")]
    [InlineData("CREDIT", -5, "amount")]
    [InlineData("CREDIT", 1500.005, "amount")]
    [InlineData("credit", 10, "type")]
    [InlineData("TRANSFER", 10, "type")]
    public async Task Post_WithAnInvalidField_ShouldReturnValidationProblem(
        string type, decimal amount, string expectedField)
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type,
            amount,
            occurredAt = "2026-08-08T14:30:00Z",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await Json(response);
        body.GetProperty("type").GetString().Should().Be("https://cashflow.dev/problems/validation-error");
        body.GetProperty("title").GetString().Should().Be("Validation failed");
        body.GetProperty("errors").TryGetProperty(expectedField, out _).Should().BeTrue();
        body.TryGetProperty("correlationId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithSeveralInvalidFields_ShouldReportAllOfThemAtOnce()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type = "TRANSFER",
            amount = -1m,
            occurredAt = "2026-08-08T14:30:00",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Validar em cascata obrigaria o cliente a um ciclo de tentativa e erro
        // por campo (§4.2).
        var errors = (await Json(response)).GetProperty("errors");
        errors.TryGetProperty("type", out _).Should().BeTrue();
        errors.TryGetProperty("amount", out _).Should().BeTrue();
        errors.TryGetProperty("occurredAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithAnInstantWithoutOffset_ShouldBeRejected()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type = "CREDIT",
            amount = 10.00m,
            occurredAt = "2026-08-08T14:30:00",
        });

        // Aceitá-lo exigiria adivinhar o fuso do cliente, e adivinhar fuso em
        // domínio financeiro produz lançamento no dia errado em silêncio (§1.3).
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("errors").TryGetProperty("occurredAt", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_WithMalformedJson_ShouldReturnMalformedProblem()
    {
        var response = await _client.PostAsync(
            "/transactions", new StringContent("{ isto não é json", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("type").GetString()
            .Should().Be("https://cashflow.dev/problems/malformed-request");
    }

    [Fact]
    public async Task Post_WithANonJsonContentType_ShouldReturnUnsupportedMediaType()
    {
        var content = new StringContent("<transaction/>", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

        var response = await _client.PostAsync("/transactions", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await Json(response)).GetProperty("type").GetString()
            .Should().Be("https://cashflow.dev/problems/unsupported-media-type");
    }

    [Fact]
    public async Task Get_WithAnUnknownId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync($"/transactions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Json(response)).GetProperty("type").GetString()
            .Should().Be("https://cashflow.dev/problems/not-found");
    }

    [Fact]
    public async Task Get_WithAnIdThatIsNotAUuid_ShouldReturnBadRequest()
    {
        // `400` e não `404`: o contrato distingue id fora do formato de id válido
        // e inexistente (§2.2).
        var response = await _client.GetAsync("/transactions/nao-e-um-uuid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_WithoutRecords_ShouldReturnAnEmptyCollection()
    {
        var response = await _client.GetAsync("/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await Json(response);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
        body.GetProperty("hasMore").GetBoolean().Should().BeFalse();
        body.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_ShouldPaginateByCursorInDescendingOrder()
    {
        foreach (var hour in new[] { 9, 10, 11 })
        {
            await _client.PostAsJsonAsync("/transactions", new
            {
                type = "CREDIT",
                amount = 10.00m,
                occurredAt = $"2026-08-08T{hour:00}:00:00Z",
            });
        }

        var first = await Json(await _client.GetAsync("/transactions?limit=2"));
        first.GetProperty("items").GetArrayLength().Should().Be(2);
        first.GetProperty("hasMore").GetBoolean().Should().BeTrue();

        var cursor = first.GetProperty("nextCursor").GetString();
        var second = await Json(await _client.GetAsync($"/transactions?limit=2&cursor={cursor}"));

        second.GetProperty("items").GetArrayLength().Should().Be(1);
        second.GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("/transactions?limit=0")]
    [InlineData("/transactions?limit=201")]
    [InlineData("/transactions?cursor=isto-nao-e-um-cursor")]
    [InlineData("/transactions?startDate=08-2026-01")]
    [InlineData("/transactions?startDate=2026-08-10&endDate=2026-08-01")]
    public async Task List_WithAnInvalidParameter_ShouldReturnBadRequest(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task List_ShouldFilterByPeriodIncludingBothEnds()
    {
        foreach (var day in new[] { "2026-08-07", "2026-08-08", "2026-08-09" })
        {
            await _client.PostAsJsonAsync("/transactions", new
            {
                type = "CREDIT",
                amount = 10.00m,
                occurredAt = $"{day}T23:59:00Z",
            });
        }

        var body = await Json(await _client.GetAsync("/transactions?startDate=2026-08-08&endDate=2026-08-08"));

        body.GetProperty("items").GetArrayLength().Should().Be(1, "endDate inclui o dia inteiro");
    }

    [Fact]
    public async Task OpenApi_ShouldDescribeTheThreeRoutesTheContractDefines()
    {
        var document = await Json(await _client.GetAsync("/openapi/v1.json"));
        var paths = document.GetProperty("paths");

        // A partir da etapa 11 a especificação é gerada do código e precisa
        // concordar com `api-contracts.md` §2 — divergência entre os dois é
        // defeito, não evolução (§8).
        paths.TryGetProperty("/transactions", out var collection).Should().BeTrue();
        collection.TryGetProperty("post", out _).Should().BeTrue();
        collection.TryGetProperty("get", out _).Should().BeTrue();

        paths.TryGetProperty("/transactions/{id}", out var single).Should().BeTrue();
        single.TryGetProperty("get", out _).Should().BeTrue();
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
