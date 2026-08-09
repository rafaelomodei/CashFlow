namespace CashFlow.Infrastructure.Messaging;

/// <summary>
/// Nomes da topologia definida em ADR-003 e em `api-contracts.md` §5.4.
///
/// São constantes, e não configuração: mudá-los quebra produtor e consumidor ao
/// mesmo tempo, e uma quebra dessas não deveria caber em uma variável de
/// ambiente. O que varia entre ambientes é o endereço do broker, não a forma da
/// topologia.
/// </summary>
public static class RabbitMqTopology
{
    public const string Exchange = "cashflow.transactions";

    public const string RoutingKey = "transaction.registered";

    public const string Queue = "consolidation.transaction-registered";

    public const string DeadLetterExchange = "cashflow.transactions.dlx";

    public const string DeadLetterQueue = "consolidation.transaction-registered.dlq";

    /// <summary>
    /// A DLQ recebe tudo que for descartado da fila principal, qualquer que seja
    /// a routing key original.
    /// </summary>
    public const string DeadLetterRoutingKey = "#";
}
