using System.Reflection;
using CashFlow.Domain.Entities;
using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;

namespace CashFlow.Domain.UnitTests.Entities;

/// <summary>
/// RF-001 e RF-002: o lançamento é a unidade do fluxo de caixa. Uma vez criado,
/// não muda (premissa P-05).
/// </summary>
[Trait("Category", "Unit")]
public class TransactionTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 8, 8, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidData_ShouldReturnTheTransaction()
    {
        var amount = Money.Create(1500.00m);

        var transaction = Transaction.Create(amount, TransactionType.Credit, OccurredAt, "Venda no balcão");

        transaction.Amount.Should().Be(amount);
        transaction.Type.Should().Be(TransactionType.Credit);
        transaction.OccurredAt.Should().Be(OccurredAt);
        transaction.Description.Should().Be("Venda no balcão");
    }

    [Fact]
    public void Create_ShouldAssignAnIdentity()
    {
        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, null);

        transaction.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldGiveEachTransactionItsOwnIdentity()
    {
        var one = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, null);
        var other = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, null);

        one.Id.Should().NotBe(other.Id);
    }

    [Fact]
    public void Create_ShouldRecordWhenTheTransactionWasRegistered()
    {
        var before = DateTimeOffset.UtcNow;

        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, null);

        transaction.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WithoutAmount_ShouldThrowArgumentNullException()
    {
        var act = () => Transaction.Create(null!, TransactionType.Credit, OccurredAt, null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithUndefinedType_ShouldThrowDomainException()
    {
        var act = () => Transaction.Create(Money.Create(1500.00m), (TransactionType)99, OccurredAt, null);

        act.Should().Throw<InvalidTransactionTypeException>()
            .WithMessage("Type must be either CREDIT or DEBIT.");
    }

    [Fact]
    public void Create_WithTheZeroInstant_ShouldThrowDomainException()
    {
        var act = () => Transaction.Create(
            Money.Create(1500.00m), TransactionType.Credit, default(DateTimeOffset), null);

        act.Should().Throw<InvalidOccurrenceDateException>()
            .WithMessage("OccurredAt is required.");
    }

    [Fact]
    public void Create_WithoutOccurredAt_ShouldUseTheRegistrationInstant()
    {
        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, null, null);

        // Exatamente igual, e não aproximadamente: o contrato promete a igualdade
        // (§2.1, premissa P-08), e é ela que permite ao cliente distinguir um
        // instante informado de um instante assumido pelo servidor.
        transaction.OccurredAt.Should().Be(transaction.CreatedAt);
    }

    [Fact]
    public void Create_WithAnOffsetOtherThanUtc_ShouldNormalizeToUtc()
    {
        // 22h em Brasília (UTC−3) é 01h do dia seguinte em UTC — a limitação de
        // fuso que a ADR-013 aceita e documenta em vez de esconder.
        var inBrasilia = new DateTimeOffset(2026, 8, 8, 22, 0, 0, TimeSpan.FromHours(-3));

        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, inBrasilia, null);

        transaction.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        transaction.OccurredAt.UtcDateTime.Should().Be(new DateTime(2026, 8, 9, 1, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutDescription_ShouldStoreItAsAbsent(string? description)
    {
        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, description);

        transaction.Description.Should().BeNull();
    }

    [Fact]
    public void Create_WithDescriptionLongerThanTheLimit_ShouldThrowDomainException()
    {
        var tooLong = new string('a', 201);

        var act = () => Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, tooLong);

        act.Should().Throw<InvalidDescriptionException>()
            .WithMessage("Description must not exceed 200 characters.");
    }

    [Fact]
    public void Create_WithDescriptionAtTheLimit_ShouldBeAccepted()
    {
        var atLimit = new string('a', 200);

        var transaction = Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, OccurredAt, atLimit);

        transaction.Description.Should().Be(atLimit);
    }

    [Fact]
    public void Transaction_ShouldNotExposeAnyWayToChangeItsStateAfterCreation()
    {
        var settableProperties = typeof(Transaction)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name);

        settableProperties.Should().BeEmpty("lançamentos são imutáveis após criados (premissa P-05)");
    }
}
