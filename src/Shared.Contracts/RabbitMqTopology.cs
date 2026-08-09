namespace Shared.Contracts;

/// <summary>
/// Nomes da topologia definida em ADR-003 e em `api-contracts.md` §5.4.
///
/// Vive aqui, e não na infraestrutura de um dos lados, porque é **contrato**: o
/// produtor publica em um exchange e o consumidor escuta em uma fila ligada a
/// ele, e uma divergência entre os dois nomes quebraria a integração em silêncio
/// — sem erro de compilação, sem exceção, apenas eventos que nunca chegam.
/// Duplicar as constantes nos dois contextos tornaria essa divergência possível.
///
/// São constantes, e não configuração: mudá-las quebra produtor e consumidor ao
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
