using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;

namespace CashFlow.Domain.Entities;

/// <summary>
/// Lançamento do fluxo de caixa (RF-001). Imutável após a criação: o MVP não
/// tem edição, exclusão nem estorno (premissa P-05).
/// </summary>
public sealed class Transaction
{
    /// <summary>Premissa P-10.</summary>
    private const int DescriptionMaxLength = 200;

    private Transaction(
        Guid id,
        Money amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        string? description,
        DateTimeOffset createdAt)
    {
        Id = id;
        Amount = amount;
        Type = type;
        OccurredAt = occurredAt;
        Description = description;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Money Amount { get; }

    public TransactionType Type { get; }

    /// <summary>
    /// Instante do lançamento, sempre em UTC. É a data que decide a qual dia o
    /// lançamento pertence na consolidação (RN-004, ADR-013).
    /// </summary>
    public DateTimeOffset OccurredAt { get; }

    public string? Description { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <param name="occurredAt">
    /// Nulo quando o cliente não informou. Nesse caso o lançamento ocorreu no
    /// instante em que foi registrado, e os dois campos recebem exatamente o
    /// mesmo valor — o contrato promete essa igualdade (§2.1, premissa P-08), e
    /// duas leituras de relógio a quebrariam por microssegundos.
    /// </param>
    public static Transaction Create(
        Money amount,
        TransactionType type,
        DateTimeOffset? occurredAt,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!Enum.IsDefined(type))
        {
            throw new InvalidTransactionTypeException();
        }

        if (occurredAt == default(DateTimeOffset))
        {
            throw new InvalidOccurrenceDateException();
        }

        var normalizedDescription = Normalize(description);
        var createdAt = DateTimeOffset.UtcNow;

        return new Transaction(
            Guid.NewGuid(),
            amount,
            type,
            occurredAt?.ToUniversalTime() ?? createdAt,
            normalizedDescription,
            createdAt);
    }

    private static string? Normalize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        if (description.Length > DescriptionMaxLength)
        {
            throw new InvalidDescriptionException(
                $"Description must not exceed {DescriptionMaxLength} characters.");
        }

        return description;
    }
}
