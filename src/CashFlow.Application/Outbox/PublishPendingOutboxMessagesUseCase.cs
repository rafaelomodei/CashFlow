using CashFlow.Application.Abstractions;

namespace CashFlow.Application.Outbox;

/// <summary>
/// UC-05 — publicar as mensagens pendentes do outbox (RNF-007, ADR-004).
///
/// A mensagem só sai de pendente após a confirmação do broker. Enquanto isso não
/// acontecer, ela continua lá — é essa teimosia que faz o evento sobreviver a um
/// broker fora do ar.
/// </summary>
public sealed class PublishPendingOutboxMessagesUseCase
{
    private readonly IOutboxRepository _outbox;
    private readonly IEventPublisher _publisher;
    private readonly IUnitOfWork _unitOfWork;

    public PublishPendingOutboxMessagesUseCase(
        IOutboxRepository outbox,
        IEventPublisher publisher,
        IUnitOfWork unitOfWork)
    {
        _outbox = outbox;
        _publisher = publisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<OutboxPublishResult> Handle(int batchSize, CancellationToken cancellationToken)
    {
        var pending = await _outbox.GetPendingAsync(batchSize, cancellationToken);
        if (pending.Count == 0)
        {
            return OutboxPublishResult.Idle;
        }

        var published = 0;
        var failed = 0;

        foreach (var message in pending)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                message.MarkAsProcessed();
                published++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Uma mensagem que falha não interrompe o lote: a próxima pode ser
                // de outro destino, e parar aqui atrasaria todas por causa de uma.
                message.RegisterFailure(exception.Message);
                failed++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new OutboxPublishResult(published, failed);
    }
}
