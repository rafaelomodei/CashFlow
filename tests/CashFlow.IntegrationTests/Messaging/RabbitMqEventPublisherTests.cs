using System.Text;
using CashFlow.Application.Outbox;
using CashFlow.Infrastructure.Messaging;
using FluentAssertions;
using RabbitMQ.Client;
using Shared.Contracts;

namespace CashFlow.IntegrationTests.Messaging;

/// <summary>
/// Publicação no RabbitMQ conforme `api-contracts.md` §5.4 e ADR-003.
/// </summary>
[Collection(nameof(MessagingCollection))]
[Trait("Category", "Integration")]
public class RabbitMqEventPublisherTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Emission = new(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly MessagingFixture _fixture;

    public RabbitMqEventPublisherTests(MessagingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await _fixture.DrainQueueAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PublishAsync_ShouldDeliverThePayloadUntouchedToTheQueue()
    {
        var message = OutboxMessage.Create(
            Guid.NewGuid(), TransactionRegisteredEvent.Type, """{"eventId":"x","data":{}}""", Emission);

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        await new RabbitMqEventPublisher(provider).PublishAsync(message, CancellationToken.None);

        var delivered = await _fixture.DrainQueueAsync();

        delivered.Should().HaveCount(1);

        // O corpo é o payload gravado no outbox, byte a byte: reserializar aqui
        // abriria espaço para o que trafega divergir do que foi auditado.
        Encoding.UTF8.GetString(delivered[0].Body.Span).Should().Be(message.Payload);
    }

    [Fact]
    public async Task PublishAsync_ShouldSetTheAmqpPropertiesTheContractDefines()
    {
        var eventId = Guid.NewGuid();
        var message = OutboxMessage.Create(eventId, TransactionRegisteredEvent.Type, "{}", Emission);

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        await new RabbitMqEventPublisher(provider).PublishAsync(message, CancellationToken.None);

        var properties = (await _fixture.DrainQueueAsync()).Single().BasicProperties;

        properties.ContentType.Should().Be("application/json");
        properties.ContentEncoding.Should().Be("utf-8");
        properties.DeliveryMode.Should().Be(DeliveryModes.Persistent);
        properties.MessageId.Should().Be(eventId.ToString());
        properties.Type.Should().Be(TransactionRegisteredEvent.Type);
        properties.Timestamp.UnixTime.Should().Be(Emission.ToUnixTimeSeconds());
        properties.Headers.Should().ContainKey("x-event-version")
            .WhoseValue.Should().Be(TransactionRegisteredEvent.Version);
    }

    [Fact]
    public async Task PublishAsync_ShouldRouteThroughTheContractExchangeAndRoutingKey()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), TransactionRegisteredEvent.Type, "{}", Emission);

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        await new RabbitMqEventPublisher(provider).PublishAsync(message, CancellationToken.None);

        var delivered = (await _fixture.DrainQueueAsync()).Single();

        delivered.Exchange.Should().Be(RabbitMqTopology.Exchange);
        delivered.RoutingKey.Should().Be(RabbitMqTopology.RoutingKey);
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowWhenNoQueueIsBoundToReceiveTheMessage()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), TransactionRegisteredEvent.Type, "{}", Emission);

        await using var provider = _fixture.CreateConnectionProvider(_fixture.BrokerOptions());
        var publisher = new RabbitMqEventPublisher(provider);

        await MessagingFixture.WithoutQueueBindingAsync(provider, async () =>
        {
            var publish = async () => await publisher.PublishAsync(message, CancellationToken.None);

            // Sem `mandatory` e sem confirmação rastreada, o exchange aceitaria a
            // mensagem, não teria para onde entregá-la e a descartaria calado — e
            // o outbox a marcaria como publicada. Perda de evento sem erro em
            // lugar nenhum é exatamente o que ADR-004 existe para impedir.
            await publish.Should().ThrowAsync<Exception>();
        });
    }

    [Fact]
    public async Task PublishAsync_ShouldThrowWhenTheBrokerIsUnreachable()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), TransactionRegisteredEvent.Type, "{}", Emission);

        await using var provider = _fixture.CreateConnectionProvider(MessagingFixture.UnreachableBrokerOptions());
        var publish = async () =>
            await new RabbitMqEventPublisher(provider).PublishAsync(message, CancellationToken.None);

        // Falhar é obrigatório: se a publicação engolisse o erro, o outbox
        // marcaria como publicada uma mensagem que ninguém recebeu (ADR-004).
        await publish.Should().ThrowAsync<Exception>();
    }
}
