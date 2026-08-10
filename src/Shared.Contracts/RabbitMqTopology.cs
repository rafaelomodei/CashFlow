namespace Shared.Contracts;

/// <summary>
/// Nomes da topologia (ADR-003, `api-contracts.md` §5.4). Vive aqui porque é
/// contrato: produtor e consumidor precisam concordar nos nomes, e duplicá-los
/// nos dois contextos tornaria possível uma divergência que quebraria a
/// integração em silêncio. São constantes, não configuração — o que varia entre
/// ambientes é o endereço do broker, não a forma da topologia.
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
