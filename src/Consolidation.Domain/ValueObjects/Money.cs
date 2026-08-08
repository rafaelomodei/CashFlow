using System.Globalization;
using Consolidation.Domain.Exceptions;

namespace Consolidation.Domain.ValueObjects;

/// <summary>
/// Quantidade monetária da consolidação: totais e saldo do dia. Diferente do
/// <c>Money</c> do contexto de lançamentos, aceita zero e valores negativos — um
/// dia sem movimento vale zero e um dia com mais débitos que créditos fecha
/// negativo (RF-004). A regra de valor positivo é do lançamento (RN-001), e é
/// cobrada onde ela vale: em <see cref="Entities.DailyBalance.Apply"/>.
/// </summary>
public sealed record Money
{
    private const int Scale = 2;

    /// <summary>Limite de <c>numeric(18,2)</c> (ADR-005).</summary>
    private const decimal MaxAmount = 9999999999999999.99m;

    private Money(decimal amount) => Amount = amount;

    public static Money Zero { get; } = Create(0m);

    public decimal Amount { get; }

    public static Money Create(decimal amount)
    {
        // Comparar com o valor arredondado detecta casas decimais significativas
        // sem rejeitar zeros à direita: 10.500 é 10.50, mas 10.555 não é.
        if (decimal.Round(amount, Scale) != amount)
        {
            throw new InvalidAmountException("Amount must have at most two decimal places.");
        }

        if (amount is > MaxAmount or < (-MaxAmount))
        {
            throw new InvalidAmountException(
                $"Amount must be between {-MaxAmount} and {MaxAmount}.");
        }

        // Somar zero com duas casas eleva a escala sem alterar o valor, de modo
        // que 1500 seja representado como 1500.00 — a forma que o contrato de API
        // e a coluna numeric(18,2) usam.
        return new Money(amount + 0.00m);
    }

    public bool IsPositive => Amount > 0m;

    public Money Add(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Create(Amount + other.Amount);
    }

    public Money Subtract(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Create(Amount - other.Amount);
    }

    public override string ToString() => Amount.ToString(CultureInfo.InvariantCulture);
}
