using FluentAssertions;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.OfxImport;

namespace MyMascada.Tests.Unit.Services;

/// <summary>
/// Verifies OFX parsing + the candidate mapping used by ImportReviewController.ConvertOfxToCandidatesAsync,
/// which was previously a NotImplementedException (caused "An error occurred while analyzing the import").
/// </summary>
public class OfxImportCandidateMappingTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "sample-bank-statement.ofx");

    private readonly OfxParserService _parser = new();

    [Fact]
    public async Task ParseOfx_FromStream_ReturnsAllTransactions()
    {
        await using var stream = File.OpenRead(FixturePath);

        var result = await _parser.ParseOfxFileAsync(stream);

        result.Success.Should().BeTrue(because: result.Message);
        result.Transactions.Should().HaveCount(5);
    }

    [Fact]
    public async Task MapToCandidates_ProducesCorrectDescriptionAmountAndType()
    {
        await using var stream = File.OpenRead(FixturePath);
        var result = await _parser.ParseOfxFileAsync(stream);

        // Mirror ConvertOfxToCandidatesAsync mapping.
        var candidates = result.Transactions.Select(txn => new
        {
            Description = !string.IsNullOrWhiteSpace(txn.Name) ? txn.Name : (txn.Memo ?? "Unknown Transaction"),
            txn.Amount,
            ExternalReferenceId = txn.TransactionId,
            Type = txn.Amount > 0 ? TransactionType.Income : TransactionType.Expense
        }).ToList();

        // No "Unknown Transaction" — every OFX row has a NAME.
        candidates.Should().NotContain(c => c.Description == "Unknown Transaction");

        var starbucks = candidates.First(c => c.Description.Contains("STARBUCKS"));
        starbucks.Amount.Should().Be(-45.50m);
        starbucks.Type.Should().Be(TransactionType.Expense);
        starbucks.ExternalReferenceId.Should().Be("2023120100001");

        var salary = candidates.First(c => c.Description.Contains("SALARY"));
        salary.Amount.Should().Be(2500.00m);
        salary.Type.Should().Be(TransactionType.Income);
    }
}
