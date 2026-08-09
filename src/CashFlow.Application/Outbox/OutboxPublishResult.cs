namespace CashFlow.Application.Outbox;

/// <summary>
/// Resultado de uma passagem do publisher. As duas contagens existem porque um
/// ciclo que não publicou nada é ambíguo sozinho: pode não haver o que publicar,
/// ou o broker pode estar fora do ar — e a diferença decide se alguém precisa
/// olhar.
/// </summary>
public sealed record OutboxPublishResult(int Published, int Failed)
{
    public static readonly OutboxPublishResult Idle = new(0, 0);

    public int Attempted => Published + Failed;
}
