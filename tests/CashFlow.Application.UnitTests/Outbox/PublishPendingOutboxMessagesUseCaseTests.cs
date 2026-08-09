using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CashFlow.Application.UnitTests.Outbox;

/// <summary>
/// UC-05 (RNF-007). O que este caso de uso precisa garantir não é que a
/// publicação funcione — é que uma falha nela não faça o evento desaparecer.
/// </summary>
[Trait("Category", "Unit")]
public class PublishPendingOutboxMessagesUseCaseTests
{
    private readonly IOutboxRepository _outbox = Substitute.For<IOutboxRepository>();
    private readonly IEventPublisher _publisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PublishPendingOutboxMessagesUseCase _useCase;

    public PublishPendingOutboxMessagesUseCaseTests()
    {
        _useCase = new PublishPendingOutboxMessagesUseCase(_outbox, _publisher, _unitOfWork);
    }

    private static OutboxMessage PendingMessage() =>
        OutboxMessage.Create(Guid.NewGuid(), "TransactionRegistered", "{}", DateTimeOffset.UtcNow);

    private void OutboxHas(params OutboxMessage[] messages) =>
        _outbox.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(messages);

    [Fact]
    public async Task Handle_ShouldPublishEveryPendingMessage()
    {
        var first = PendingMessage();
        var second = PendingMessage();
        OutboxHas(first, second);

        await _useCase.Handle(batchSize: 10, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(first, Arg.Any<CancellationToken>());
        await _publisher.Received(1).PublishAsync(second, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAskTheOutboxForTheRequestedBatchSize()
    {
        OutboxHas();

        await _useCase.Handle(batchSize: 25, CancellationToken.None);

        await _outbox.Received(1).GetPendingAsync(25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AfterTheBrokerConfirms_ShouldMarkTheMessageAsProcessed()
    {
        var message = PendingMessage();
        OutboxHas(message);

        var result = await _useCase.Handle(batchSize: 10, CancellationToken.None);

        message.ProcessedAt.Should().NotBeNull();
        message.Error.Should().BeNull();
        result.Published.Should().Be(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPublishingFails_ShouldKeepTheMessagePending()
    {
        var message = PendingMessage();
        OutboxHas(message);
        _publisher.PublishAsync(message, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var result = await _useCase.Handle(batchSize: 10, CancellationToken.None);

        message.ProcessedAt.Should().BeNull("evento não publicado não pode ser dado como publicado");
        result.Published.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenPublishingFails_ShouldRecordTheAttemptAndTheError()
    {
        var message = PendingMessage();
        OutboxHas(message);
        _publisher.PublishAsync(message, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        await _useCase.Handle(batchSize: 10, CancellationToken.None);

        message.Attempts.Should().Be(1);
        message.Error.Should().Contain("broker unreachable");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOneMessageFails_ShouldStillPublishTheOthers()
    {
        var failing = PendingMessage();
        var healthy = PendingMessage();
        OutboxHas(failing, healthy);
        _publisher.PublishAsync(failing, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var result = await _useCase.Handle(batchSize: 10, CancellationToken.None);

        await _publisher.Received(1).PublishAsync(healthy, Arg.Any<CancellationToken>());
        healthy.ProcessedAt.Should().NotBeNull();
        failing.ProcessedAt.Should().BeNull();
        result.Published.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReportHowManyFailed_SoTheCycleCanBeLogged()
    {
        var failing = PendingMessage();
        var healthy = PendingMessage();
        OutboxHas(failing, healthy);
        _publisher.PublishAsync(failing, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        var result = await _useCase.Handle(batchSize: 10, CancellationToken.None);

        // Sem a contagem de falhas, um ciclo que não publicou nada seria
        // indistinguível de um ciclo sem nada a publicar — e é a diferença entre
        // o sistema estar ocioso e estar quebrado.
        result.Published.Should().Be(1);
        result.Failed.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNothingPending_ShouldNotTouchThePublisherNorTheDatabase()
    {
        OutboxHas();

        var result = await _useCase.Handle(batchSize: 10, CancellationToken.None);

        result.Published.Should().Be(0);
        await _publisher.DidNotReceive().PublishAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
