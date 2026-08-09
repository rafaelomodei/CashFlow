using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace EndToEndTests;

/// <summary>
/// O fluxo que o desafio pede, do começo ao fim:
///
/// <code>
/// POST /transactions → outbox → RabbitMQ → worker → GET /daily-balances/{date}
/// </code>
///
/// Nenhum dos dois contextos conhece o outro. O que os liga é o evento, e é isso
/// que este teste verifica de fato.
/// </summary>
[Collection(nameof(CashFlowSystemCollection))]
[Trait("Category", "Integration")]
public class TransactionToDailyBalanceTests
{
    private const string Day = "2026-09-15";

    private readonly CashFlowSystemFixture _system;

    public TransactionToDailyBalanceTests(CashFlowSystemFixture system) => _system = system;

    [Fact]
    public async Task ARegisteredTransaction_ShouldEventuallyShowUpInTheDailyBalance()
    {
        await Register("CREDIT", 1500.00m, $"{Day}T14:30:00Z");
        await Register("DEBIT", 700.00m, $"{Day}T15:00:00Z");

        // "Eventually" é o contrato, não uma concessão do teste: a consistência é
        // eventual por decisão (ADR-006), e o `updatedAt` da resposta é a evidência
        // que o cliente tem dessa janela.
        var balance = await WaitUntilConsolidated(Day, expected: 800.00m);

        balance.GetProperty("totalCredits").GetDecimal().Should().Be(1500.00m);
        balance.GetProperty("totalDebits").GetDecimal().Should().Be(700.00m);
        balance.GetProperty("balance").GetDecimal().Should().Be(800.00m);
        balance.GetProperty("updatedAt").GetString().Should().EndWith("Z");
    }

    [Fact]
    public async Task ARetroactiveTransaction_ShouldLandOnTheDayItHappened()
    {
        const string pastDay = "2026-01-05";

        await Register("CREDIT", 250.00m, $"{pastDay}T08:00:00Z");

        // O worker consolida por `data.occurredAt`, não pelo instante da emissão.
        // Consolidar pelo envelope colocaria todo lançamento retroativo no dia em
        // que o evento foi publicado — um saldo errado que não falha em lugar
        // nenhum (contrato §5.2, RN-004).
        var balance = await WaitUntilConsolidated(pastDay, expected: 250.00m);

        balance.GetProperty("date").GetString().Should().Be(pastDay);
    }

    private async Task Register(string type, decimal amount, string occurredAt)
    {
        var response = await _system.CashFlow.PostAsJsonAsync(
            "/transactions", new { type, amount, occurredAt });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<JsonElement> WaitUntilConsolidated(string day, decimal expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        JsonElement balance = default;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var response = await _system.Consolidation.GetAsync($"/daily-balances/{day}");
            balance = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            if (balance.GetProperty("balance").GetDecimal() == expected)
            {
                return balance;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException(
            $"O saldo de {day} não convergiu para {expected}. Último valor: {balance}");
    }
}
