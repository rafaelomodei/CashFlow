using CashFlow.Domain.Exceptions;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;

namespace CashFlow.Domain.UnitTests.ValueObjects;

/// <summary>
/// RN-002 e RN-003: o lançamento é crédito ou débito, e é o tipo — nunca o
/// valor — que decide o sinal aplicado ao saldo (ADR-013).
/// </summary>
[Trait("Category", "Unit")]
public class TransactionTypeTests
{
    [Fact]
    public void ApplyTo_WhenTypeIsCredit_ShouldReturnPositiveValue()
    {
        var amount = Money.Create(100.00m);

        var effect = TransactionType.Credit.ApplyTo(amount);

        effect.Should().Be(100.00m);
    }

    [Fact]
    public void ApplyTo_WhenTypeIsDebit_ShouldReturnNegativeValue()
    {
        var amount = Money.Create(100.00m);

        var effect = TransactionType.Debit.ApplyTo(amount);

        effect.Should().Be(-100.00m);
    }

    [Fact]
    public void ApplyTo_WithUndefinedType_ShouldThrowDomainException()
    {
        var undefined = (TransactionType)99;

        var act = () => undefined.ApplyTo(Money.Create(100.00m));

        act.Should().Throw<InvalidTransactionTypeException>()
            .WithMessage("Type must be either CREDIT or DEBIT.");
    }

    [Theory]
    [InlineData("CREDIT", TransactionType.Credit)]
    [InlineData("DEBIT", TransactionType.Debit)]
    public void Parse_WithASupportedValue_ShouldReturnTheType(string value, TransactionType expected)
    {
        var type = TransactionTypes.Parse(value);

        type.Should().Be(expected);
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("Debit")]
    [InlineData("TRANSFER")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Parse_WithAnUnsupportedValue_ShouldThrowDomainException(string? value)
    {
        var act = () => TransactionTypes.Parse(value);

        act.Should().Throw<InvalidTransactionTypeException>()
            .WithMessage("Type must be either CREDIT or DEBIT.");
    }

    [Theory]
    [InlineData(TransactionType.Credit, "CREDIT")]
    [InlineData(TransactionType.Debit, "DEBIT")]
    public void ToContractValue_ShouldReturnTheNameUsedByTheContract(TransactionType type, string expected)
    {
        var value = type.ToContractValue();

        value.Should().Be(expected);
    }
}
