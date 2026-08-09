using CashFlow.Application.Abstractions;
using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CashFlow.Infrastructure.Persistence;

/// <summary>
/// Persistência de lançamentos sobre PostgreSQL (ADR-005). Nenhum método comita:
/// quem fecha a transação é <see cref="UnitOfWork"/> (ADR-004).
/// </summary>
public sealed class TransactionRepository : ITransactionRepository
{
    private readonly CashFlowDbContext _context;

    public TransactionRepository(CashFlowDbContext context) => _context = context;

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken) =>
        await _context.Transactions.AddAsync(transaction, cancellationToken);

    // Sem rastreamento nas leituras: lançamento é imutável (premissa P-05), e
    // manter um snapshot para detectar alteração que não pode acontecer é custo
    // sem contrapartida.
    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> ListAsync(
        TransactionListFilter filter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (sql, parameters) = BuildPageQuery(filter);

        return await _context.Transactions
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// SQL literal em vez de LINQ por um motivo só: a comparação de tupla
    /// <c>(occurred_at, id) &lt; (@o, @i)</c> que ADR-014 escolheu não é
    /// traduzida pelo provedor. Reescrevê-la como
    /// <c>a &lt; @a OR (a = @a AND b &lt; @b)</c> devolveria o mesmo resultado por
    /// um caminho que o planejador nem sempre resolve pelo índice — que é a razão
    /// de o índice existir. Todo valor viaja como parâmetro; as únicas partes
    /// montadas em C# são literais fixos.
    /// </summary>
    private static (string Sql, NpgsqlParameter[] Parameters) BuildPageQuery(TransactionListFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new List<NpgsqlParameter>();

        if (filter.From is not null)
        {
            conditions.Add("occurred_at >= @from");
            parameters.Add(new NpgsqlParameter("from", filter.From.Value));
        }

        if (filter.ToExclusive is not null)
        {
            conditions.Add("occurred_at < @to_exclusive");
            parameters.Add(new NpgsqlParameter("to_exclusive", filter.ToExclusive.Value));
        }

        if (filter.CursorOccurredAt is not null && filter.CursorId is not null)
        {
            conditions.Add("(occurred_at, id) < (@cursor_occurred_at, @cursor_id)");
            parameters.Add(new NpgsqlParameter("cursor_occurred_at", filter.CursorOccurredAt.Value));
            parameters.Add(new NpgsqlParameter("cursor_id", filter.CursorId.Value));
        }

        parameters.Add(new NpgsqlParameter("page_limit", filter.Limit));

        var where = conditions.Count == 0 ? string.Empty : $" WHERE {string.Join(" AND ", conditions)}";
        var sql = $"SELECT * FROM transactions{where} ORDER BY occurred_at DESC, id DESC LIMIT @page_limit";

        return (sql, [.. parameters]);
    }
}
