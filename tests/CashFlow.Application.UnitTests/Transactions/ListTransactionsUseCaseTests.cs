using CashFlow.Application.Abstractions;
using CashFlow.Application.Exceptions;
using CashFlow.Application.Transactions;
using CashFlow.Domain.Entities;
using CashFlow.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace CashFlow.Application.UnitTests.Transactions;

/// <summary>
/// UC-03 (RF-003). Paginação por cursor (keyset), não por offset — ADR-014.
/// </summary>
[Trait("Category", "Unit")]
public class ListTransactionsUseCaseTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ListTransactionsUseCase _useCase;

    public ListTransactionsUseCaseTests()
    {
        _useCase = new ListTransactionsUseCase(_transactions);
    }

    private static Transaction TransactionAt(DateTimeOffset occurredAt) =>
        Transaction.Create(Money.Create(1500.00m), TransactionType.Credit, occurredAt, null);

    private void RepositoryReturns(params Transaction[] transactions) =>
        _transactions.ListAsync(Arg.Any<TransactionListFilter>(), Arg.Any<CancellationToken>())
            .Returns(transactions);

    private TransactionListFilter CapturedFilter()
    {
        var call = _transactions.ReceivedCalls().Single();

        return (TransactionListFilter)call.GetArguments()[0]!;
    }

    [Fact]
    public async Task Handle_WithoutACursor_ShouldAskForTheFirstPage()
    {
        RepositoryReturns();

        await _useCase.Handle(new ListTransactionsQuery(null, null, null, null), CancellationToken.None);

        var filter = CapturedFilter();
        filter.CursorOccurredAt.Should().BeNull();
        filter.CursorId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithoutALimit_ShouldUseTheDefaultOfFifty()
    {
        RepositoryReturns();

        await _useCase.Handle(new ListTransactionsQuery(null, null, null, null), CancellationToken.None);

        CapturedFilter().Limit.Should().Be(51,
            "a consulta pede um registro a mais para saber se existe página seguinte");
    }

    [Fact]
    public async Task Handle_ShouldReturnAtMostTheRequestedLimit()
    {
        RepositoryReturns(TransactionAt(Noon), TransactionAt(Noon.AddMinutes(-1)), TransactionAt(Noon.AddMinutes(-2)));

        var page = await _useCase.Handle(new ListTransactionsQuery(2, null, null, null), CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.HasMore.Should().BeTrue();
        page.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_OnTheLastPage_ShouldReportThatThereIsNothingLeft()
    {
        RepositoryReturns(TransactionAt(Noon), TransactionAt(Noon.AddMinutes(-1)));

        var page = await _useCase.Handle(new ListTransactionsQuery(2, null, null, null), CancellationToken.None);

        page.Items.Should().HaveCount(2);
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithACursor_ShouldResumeFromThatPositionWithoutRepeatingOrSkipping()
    {
        var first = TransactionAt(Noon);
        var second = TransactionAt(Noon.AddMinutes(-1));
        RepositoryReturns(first, second);

        var page = await _useCase.Handle(new ListTransactionsQuery(1, null, null, null), CancellationToken.None);
        _transactions.ClearReceivedCalls();
        RepositoryReturns(second);
        await _useCase.Handle(new ListTransactionsQuery(1, page.NextCursor, null, null), CancellationToken.None);

        var filter = CapturedFilter();
        filter.CursorOccurredAt.Should().Be(first.OccurredAt, "a próxima página começa depois do último item devolvido");
        filter.CursorId.Should().Be(first.Id);
    }

    [Fact]
    public async Task Handle_ShouldBreakTiesById_WhenTwoTransactionsShareTheSameInstant()
    {
        var first = TransactionAt(Noon);
        var second = TransactionAt(Noon);
        RepositoryReturns(first, second);

        var page = await _useCase.Handle(new ListTransactionsQuery(1, null, null, null), CancellationToken.None);
        _transactions.ClearReceivedCalls();
        RepositoryReturns(second);
        await _useCase.Handle(new ListTransactionsQuery(1, page.NextCursor, null, null), CancellationToken.None);

        var filter = CapturedFilter();
        filter.CursorOccurredAt.Should().Be(first.OccurredAt);
        filter.CursorId.Should().Be(first.Id, "sem desempate por id a página seguinte pularia ou repetiria registros");
    }

    [Fact]
    public async Task Handle_WithoutResults_ShouldReturnAnEmptyPageInsteadOfFailing()
    {
        RepositoryReturns();

        var page = await _useCase.Handle(new ListTransactionsQuery(null, null, null, null), CancellationToken.None);

        page.Items.Should().BeEmpty();
        page.NextCursor.Should().BeNull();
        page.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithAPeriod_ShouldFilterFromTheStartOfTheFirstDayToTheEndOfTheLast()
    {
        RepositoryReturns();

        await _useCase.Handle(
            new ListTransactionsQuery(null, null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 8)),
            CancellationToken.None);

        var filter = CapturedFilter();
        filter.From.Should().Be(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        filter.ToExclusive.Should().Be(new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero),
            "endDate é inclusivo, e comparar uma data contra um instante excluiria o próprio dia");
    }

    [Fact]
    public async Task Handle_WithOnlyOneEndOfThePeriod_ShouldLeaveTheOtherOpen()
    {
        RepositoryReturns();

        await _useCase.Handle(
            new ListTransactionsQuery(null, null, new DateOnly(2026, 8, 1), null),
            CancellationToken.None);

        var filter = CapturedFilter();
        filter.From.Should().Be(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        filter.ToExclusive.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task Handle_WithALimitOutsideTheAllowedRange_ShouldBeRejected(int limit)
    {
        var act = () => _useCase.Handle(new ListTransactionsQuery(limit, null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidQueryException>()
            .WithMessage("Limit must be between 1 and 200.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public async Task Handle_WithALimitAtTheBoundary_ShouldBeAccepted(int limit)
    {
        RepositoryReturns();

        var act = () => _useCase.Handle(new ListTransactionsQuery(limit, null, null, null), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("eyJvIjoi")]
    [InlineData("")]
    public async Task Handle_WithAnInvalidCursor_ShouldBeRejected(string cursor)
    {
        var act = () => _useCase.Handle(new ListTransactionsQuery(null, cursor, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidQueryException>()
            .WithMessage("Cursor is invalid.");
    }

    [Fact]
    public async Task Handle_WithAStartDateAfterTheEndDate_ShouldBeRejected()
    {
        var act = () => _useCase.Handle(
            new ListTransactionsQuery(null, null, new DateOnly(2026, 8, 9), new DateOnly(2026, 8, 8)),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidQueryException>()
            .WithMessage("StartDate must not be later than endDate.");
    }

    [Fact]
    public async Task Handle_ShouldReturnAnOpaqueCursor()
    {
        RepositoryReturns(TransactionAt(Noon), TransactionAt(Noon.AddMinutes(-1)));

        var page = await _useCase.Handle(new ListTransactionsQuery(1, null, null, null), CancellationToken.None);

        page.NextCursor.Should().NotBeNullOrEmpty();
        page.NextCursor.Should().NotContain("=", "base64url é usado sem padding");
        page.NextCursor.Should().NotContain("+").And.NotContain("/");
    }
}
