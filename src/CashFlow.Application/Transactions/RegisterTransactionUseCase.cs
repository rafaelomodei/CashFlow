using System.Text.Json;
using CashFlow.Application.Abstractions;
using CashFlow.Application.Outbox;
using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using Shared.Contracts;

namespace CashFlow.Application.Transactions;

/// <summary>
/// UC-01 — registrar um lançamento (RF-001, RF-002). A publicação do evento é
/// assíncrona, via outbox (ADR-004): a ausência de broker aqui é RNF-001.
/// </summary>
public sealed class RegisterTransactionUseCase
{
    private readonly ITransactionRepository _transactions;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterTransactionUseCase(
        ITransactionRepository transactions,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _transactions = transactions;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<TransactionDto> Handle(RegisterTransactionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var transaction = Transaction.Create(
            Money.Create(command.Amount),
            TransactionTypes.Parse(command.Type),
            command.OccurredAt,
            command.Description);

        await _transactions.AddAsync(transaction, cancellationToken);
        await _outbox.AddAsync(BuildEvent(transaction, command.CorrelationId), cancellationToken);

        // Lançamento e evento no mesmo commit: ou existem os dois, ou nenhum (ADR-004).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return TransactionDto.From(transaction);
    }

    private static OutboxMessage BuildEvent(Transaction transaction, Guid correlationId)
    {
        var emittedAt = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();

        var integrationEvent = new TransactionRegisteredEvent
        {
            EventId = eventId,
            OccurredAt = emittedAt,
            CorrelationId = correlationId,
            Data = new TransactionRegisteredData
            {
                TransactionId = transaction.Id,
                Type = transaction.Type.ToContractValue(),
                Amount = transaction.Amount.Amount,
                OccurredAt = transaction.OccurredAt,
            },
        };

        return OutboxMessage.Create(
            eventId,
            TransactionRegisteredEvent.Type,
            JsonSerializer.Serialize(integrationEvent, IntegrationEvents.SerializerOptions),
            emittedAt,
            correlationId);
    }
}
