namespace CashFlow.Application.Exceptions;

/// <summary>
/// Parâmetro de consulta fora do que o contrato aceita. É distinto de violação
/// de regra de negócio: não diz que o pedido é inválido para o domínio, e sim
/// que a pergunta não pode ser feita dessa forma. A borda HTTP traduz os dois
/// em `400`, por caminhos diferentes.
/// </summary>
public sealed class InvalidQueryException : Exception
{
    public InvalidQueryException(string message) : base(message)
    {
    }
}
