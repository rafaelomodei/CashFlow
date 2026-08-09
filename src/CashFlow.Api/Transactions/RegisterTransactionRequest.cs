using System.Globalization;
using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;

namespace CashFlow.Api.Transactions;

/// <summary>
/// Corpo de `POST /transactions` (§2.1).
///
/// `occurredAt` chega como texto de propósito. Desserializado direto para
/// `DateTimeOffset`, um instante sem offset — `2026-08-08T14:30:00` — seria aceito
/// e interpretado no fuso do servidor, e o contrato manda rejeitá-lo (§1.3):
/// adivinhar fuso em domínio financeiro coloca o lançamento no dia errado sem
/// falhar em lugar nenhum.
/// </summary>
public sealed record RegisterTransactionRequest(
    string? Type,
    decimal? Amount,
    string? OccurredAt,
    string? Description)
{
    public bool TryValidate(
        out DateTimeOffset? occurredAt,
        out Dictionary<string, string[]> errors)
    {
        occurredAt = null;
        errors = [];

        Collect(errors, "type", () =>
        {
            if (string.IsNullOrWhiteSpace(Type))
            {
                throw new InvalidTransactionTypeException();
            }

            TransactionTypes.Parse(Type);
        });

        Collect(errors, "amount", () =>
        {
            if (Amount is null)
            {
                throw new InvalidAmountException("Amount is required.");
            }

            Money.Create(Amount.Value);
        });

        if (OccurredAt is not null && !TryParseInstant(OccurredAt, out occurredAt))
        {
            errors["occurredAt"] = ["OccurredAt must be an ISO 8601 instant with an explicit offset."];
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Cada campo é validado de forma independente, e não em cascata: o contrato
    /// devolve **todos** os campos inválidos de uma vez (§4.2), e validar em
    /// cascata obrigaria o cliente a um ciclo de tentativa e erro por campo.
    ///
    /// Quem decide a regra continua sendo o domínio — o que acontece aqui é
    /// perguntar a ele campo a campo, em vez de deixá-lo parar no primeiro erro.
    /// Reescrever as regras aqui criaria duas verdades que divergem na primeira
    /// mudança.
    /// </summary>
    private static void Collect(Dictionary<string, string[]> errors, string field, Action validate)
    {
        try
        {
            validate();
        }
        catch (DomainException exception)
        {
            errors[field] = [exception.Message];
        }
    }

    /// <summary>
    /// Exige offset explícito. `DateTimeOffset.TryParse` aceitaria a forma sem
    /// offset assumindo o fuso local, que é exatamente o que o contrato proíbe —
    /// daí a checagem sobre o texto antes da conversão.
    /// </summary>
    private static bool TryParseInstant(string value, out DateTimeOffset? instant)
    {
        instant = null;

        if (!DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return false;
        }

        var timePart = value[(value.IndexOf('T', StringComparison.Ordinal) + 1)..];
        var hasOffset = value.Contains('T', StringComparison.Ordinal)
            && (timePart.EndsWith('Z') || timePart.Contains('+', StringComparison.Ordinal)
                || timePart.Contains('-', StringComparison.Ordinal));

        if (!hasOffset)
        {
            return false;
        }

        instant = parsed.ToUniversalTime();

        return true;
    }
}
