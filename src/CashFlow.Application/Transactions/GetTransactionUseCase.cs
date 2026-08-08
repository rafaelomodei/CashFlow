using CashFlow.Application.Abstractions;

namespace CashFlow.Application.Transactions;

/// <summary>
/// UC-06 — consultar um lançamento por id (RF-003). Existe para dar destino real
/// ao header <c>Location</c> do `201 Created`.
/// </summary>
public sealed class GetTransactionUseCase
{
    private readonly ITransactionRepository _transactions;

    public GetTransactionUseCase(ITransactionRepository transactions) => _transactions = transactions;

    /// <summary>
    /// Nulo quando o lançamento não existe. Ausência é resposta legítima da
    /// consulta, não falha — quem a traduz em `404` é a borda HTTP.
    /// </summary>
    public async Task<TransactionDto?> Handle(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await _transactions.GetByIdAsync(id, cancellationToken);

        return transaction is null ? null : TransactionDto.From(transaction);
    }
}
