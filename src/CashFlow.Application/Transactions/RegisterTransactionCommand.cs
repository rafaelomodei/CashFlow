namespace CashFlow.Application.Transactions;

/// <summary>
/// Entrada de UC-01. <paramref name="Type"/> chega como texto porque é assim que
/// o contrato o transporta — validá-lo é trabalho do domínio, não de quem monta
/// o comando.
/// </summary>
/// <param name="OccurredAt">
/// Nulo quando o cliente não informou: o instante passa a ser o do servidor, em
/// UTC (premissa P-08).
/// </param>
public sealed record RegisterTransactionCommand(
    decimal Amount,
    string Type,
    DateTimeOffset? OccurredAt,
    string? Description,
    Guid CorrelationId);
