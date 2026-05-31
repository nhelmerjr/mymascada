using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reports.Queries;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reports;

/// <summary>
/// Verifies the optional CategoryIds filter on the analytics report queries: when set, only
/// transactions in the selected categories contribute to the computed metrics; when null/empty,
/// all categories are considered (default behaviour).
/// </summary>
public class ReportsCategoryFilterTests
{
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    private const int GroceriesId = 10;
    private const int RentId = 20;
    private const int SalaryId = 30;

    private static Category Cat(int id, string name) => new() { Id = id, Name = name, Color = "#abcdef" };

    private List<Transaction> SampleTransactions()
    {
        var date = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        return new List<Transaction>
        {
            new() { Amount = -100m, TransactionDate = date, CategoryId = GroceriesId, Category = Cat(GroceriesId, "Groceries") },
            new() { Amount = -500m, TransactionDate = date, CategoryId = RentId, Category = Cat(RentId, "Rent") },
            new() { Amount = 2000m, TransactionDate = date, CategoryId = SalaryId, Category = Cat(SalaryId, "Salary") },
        };
    }

    private void StubRepository(List<Transaction> transactions)
    {
        _transactionRepository
            .GetByDateRangeAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);
    }

    // ── Monthly summary ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMonthlySummary_WithCategoryFilter_OnlyCountsSelectedCategories()
    {
        StubRepository(SampleTransactions());
        var handler = new GetMonthlySummaryQueryHandler(_transactionRepository);

        var result = await handler.Handle(new GetMonthlySummaryQuery
        {
            UserId = _userId,
            Year = 2026,
            Month = 3,
            CategoryIds = new List<int> { GroceriesId } // only Groceries
        }, CancellationToken.None);

        result.TotalExpenses.Should().Be(100m, "only the Groceries expense is in scope");
        result.TotalIncome.Should().Be(0m, "Salary is excluded by the filter");
        result.TopCategories.Should().ContainSingle().Which.CategoryId.Should().Be(GroceriesId);
    }

    [Fact]
    public async Task GetMonthlySummary_WithoutCategoryFilter_CountsEverything()
    {
        StubRepository(SampleTransactions());
        var handler = new GetMonthlySummaryQueryHandler(_transactionRepository);

        var result = await handler.Handle(new GetMonthlySummaryQuery
        {
            UserId = _userId,
            Year = 2026,
            Month = 3,
            CategoryIds = null
        }, CancellationToken.None);

        result.TotalExpenses.Should().Be(600m);
        result.TotalIncome.Should().Be(2000m);
        result.TopCategories.Should().HaveCount(2);
    }

    // ── Analytics summary ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAnalyticsSummary_WithCategoryFilter_AffectsIncomeAndExpenses()
    {
        StubRepository(SampleTransactions());
        var handler = new GetAnalyticsSummaryQueryHandler(_transactionRepository);

        var result = await handler.Handle(new GetAnalyticsSummaryQuery
        {
            UserId = _userId,
            Period = "all",
            CategoryIds = new List<int> { GroceriesId, RentId } // expenses only, no income category
        }, CancellationToken.None);

        result.TotalExpenses.Should().Be(600m);
        result.TotalIncome.Should().Be(0m, "no income category is selected");
    }

    [Fact]
    public async Task GetAnalyticsSummary_WithoutCategoryFilter_CountsEverything()
    {
        StubRepository(SampleTransactions());
        var handler = new GetAnalyticsSummaryQueryHandler(_transactionRepository);

        var result = await handler.Handle(new GetAnalyticsSummaryQuery
        {
            UserId = _userId,
            Period = "all",
            CategoryIds = null
        }, CancellationToken.None);

        result.TotalIncome.Should().Be(2000m);
        result.TotalExpenses.Should().Be(600m);
    }
}
