using CashFlow.Application.Abstractions;
using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace CashFlow.Application.UnitTests.Transactions;

/// <summary>
/// UC-06 (RF-003). Lançamento inexistente não é falha da aplicação — é ausência,
/// e quem traduz ausência em `404` é a borda HTTP.
/// </summary>
[Trait("Category", "Unit")]
public class GetTransactionUseCaseTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly GetTransactionUseCase _useCase;

    public GetTransactionUseCaseTests()
    {
        _useCase = new GetTransactionUseCase(_transactions);
    }

    [Fact]
    public async Task Handle_WhenTheTransactionExists_ShouldReturnIt()
    {
        var transaction = Transaction.Create(
            Money.Create(1500.00m),
            TransactionType.Credit,
            new DateTimeOffset(2026, 8, 8, 14, 30, 0, TimeSpan.Zero),
            "Venda no balcão");
        _transactions.GetByIdAsync(transaction.Id, Arg.Any<CancellationToken>()).Returns(transaction);

        var result = await _useCase.Handle(transaction.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(transaction.Id);
        result.Type.Should().Be("CREDIT");
        result.Amount.Should().Be(1500.00m);
        result.Description.Should().Be("Venda no balcão");
    }

    [Fact]
    public async Task Handle_WhenTheTransactionDoesNotExist_ShouldReturnNullInsteadOfFailing()
    {
        _transactions.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Transaction?)null);

        var result = await _useCase.Handle(Guid.CreateVersion7(), CancellationToken.None);

        result.Should().BeNull();
    }
}
