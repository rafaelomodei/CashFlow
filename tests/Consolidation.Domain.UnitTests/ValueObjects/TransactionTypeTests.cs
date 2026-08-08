using Consolidation.Domain.Exceptions;
using Consolidation.Domain.ValueObjects;
using FluentAssertions;

namespace Consolidation.Domain.UnitTests.ValueObjects;

/// <summary>
/// RN-002 na fronteira de entrada da consolidação: o tipo chega como texto no
/// evento e precisa virar tipo do domínio antes de tocar o saldo.
/// </summary>
[Trait("Category", "Unit")]
public class TransactionTypeTests
{
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
    [InlineData(null)]
    public void Parse_WithAnUnsupportedValue_ShouldThrowDomainException(string? value)
    {
        var act = () => TransactionTypes.Parse(value);

        act.Should().Throw<InvalidTransactionTypeException>()
            .WithMessage("Type must be either CREDIT or DEBIT.");
    }
}
