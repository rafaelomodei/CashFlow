using Consolidation.Application.Abstractions;
using Consolidation.Application.Idempotency;
using Consolidation.Domain.Entities;
using Consolidation.Domain.ValueObjects;
using Shared.Contracts;

namespace Consolidation.Application.Balances;

/// <summary>
/// UC-04 — aplicar um lançamento ao saldo do seu dia (RF-004, RNF-008).
///
/// Recebe o evento como ele chega do contrato: traduzi-lo para um comando
/// idêntico campo a campo seria indireção sem ganho. O que este caso de uso
/// protege é a soma — aplicar duas vezes não falha, apenas produz um saldo
/// errado que parece certo (ADR-007).
/// </summary>
public sealed class ConsolidateTransactionUseCase
{
    private readonly IDailyBalanceRepository _balances;
    private readonly IProcessedEventRepository _processedEvents;
    private readonly IUnitOfWork _unitOfWork;

    public ConsolidateTransactionUseCase(
        IDailyBalanceRepository balances,
        IProcessedEventRepository processedEvents,
        IUnitOfWork unitOfWork)
    {
        _balances = balances;
        _processedEvents = processedEvents;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(TransactionRegisteredEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (await _processedEvents.HasProcessedAsync(integrationEvent.EventId, cancellationToken))
        {
            return;
        }

        var type = TransactionTypes.Parse(integrationEvent.Data.Type);
        var amount = Money.Create(integrationEvent.Data.Amount);

        // O dia vem de data.occurredAt — o fato econômico —, nunca do instante da
        // emissão: consolidar pelo envelope colocaria todo lançamento retroativo
        // no dia em que o evento foi publicado (RN-004, contrato §5.2).
        var day = DailyBalance.DayOf(integrationEvent.Data.OccurredAt);

        var balance = await _balances.GetAsync(day, cancellationToken);
        if (balance is null)
        {
            balance = DailyBalance.Empty(day);
            await _balances.AddAsync(balance, cancellationToken);
        }

        balance.Apply(type, amount);
        await _processedEvents.AddAsync(ProcessedEvent.Now(integrationEvent.EventId), cancellationToken);

        // Efeito e marcação no mesmo commit: separá-los abriria a janela em que o
        // saldo muda sem que o evento conste como processado — e a próxima
        // reentrega somaria de novo.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
