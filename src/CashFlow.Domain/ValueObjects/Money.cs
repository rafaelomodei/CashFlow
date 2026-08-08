using CashFlow.Domain.Exceptions;

namespace CashFlow.Domain.ValueObjects;

/// <summary>
/// Valor monetário de um lançamento: sempre positivo, com no máximo duas casas
/// decimais (RN-001, ADR-013). O sinal nunca é armazenado aqui — ele deriva do
/// <see cref="TransactionType"/> (RN-003).
/// </summary>
public sealed record Money
{
    private const int Scale = 2;

    /// <summary>Limite de <c>numeric(18,2)</c> (ADR-005).</summary>
    private const decimal MaxAmount = 9999999999999999.99m;

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static Money Create(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new InvalidAmountException("Amount must be greater than zero.");
        }

        // Comparar com o valor arredondado detecta casas decimais significativas
        // sem rejeitar zeros à direita: 10.500 é 10.50, mas 10.555 não é.
        if (decimal.Round(amount, Scale) != amount)
        {
            throw new InvalidAmountException("Amount must have at most two decimal places.");
        }

        if (amount > MaxAmount)
        {
            throw new InvalidAmountException($"Amount must not exceed {MaxAmount}.");
        }

        // Somar zero com duas casas eleva a escala sem alterar o valor, de modo
        // que 1500 seja representado como 1500.00 — a forma que o contrato de API
        // e a coluna numeric(18,2) usam.
        return new Money(amount + 0.00m);
    }

    public override string ToString() => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
