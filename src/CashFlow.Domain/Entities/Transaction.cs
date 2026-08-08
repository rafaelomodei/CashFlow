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

    public static Transaction Create(
        Money amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        string? description)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!Enum.IsDefined(type))
        {
            throw new InvalidTransactionTypeException();
        }

        if (occurredAt == default)
        {
            throw new InvalidOccurrenceDateException();
        }

        var normalizedDescription = Normalize(description);

        // Identificador ordenável no tempo: o índice de paginação é
        // (occurred_at DESC, id DESC), e um id sequencial evita a fragmentação
        // que um GUID aleatório causaria nesse índice (ADR-014).
        return new Transaction(
            Guid.CreateVersion7(),
            amount,
            type,
            occurredAt.ToUniversalTime(),
            normalizedDescription,
            DateTimeOffset.UtcNow);
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
