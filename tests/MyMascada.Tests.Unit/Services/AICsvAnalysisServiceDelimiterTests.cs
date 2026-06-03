using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.CsvImport;
using NSubstitute;

namespace MyMascada.Tests.Unit.Services;

/// <summary>
/// Regression tests for tab/semicolon-delimited (non-comma) bank exports such as
/// Brazilian Banco Inter statements, which previously collapsed into a single column
/// and made AI column mapping impossible.
/// </summary>
public class AICsvAnalysisServiceDelimiterTests
{
    private readonly ILlmCategorizationService _llm = Substitute.For<ILlmCategorizationService>();
    private readonly IFeatureFlags _featureFlags = Substitute.For<IFeatureFlags>();
    private readonly AICsvAnalysisService _sut;

    // Real Banco Inter layout: TAB-separated, pt-BR decimal comma.
    private const string TabDelimitedCsv =
        "Data\tTipo\tDescricao\tValor\tSaldo\n" +
        "28/05/2026\tPix enviado\tAmazoncombr\t-80,49\t188,9\n" +
        "26/05/2026\tCrédito B3\tNota Bov\t847,2\t380,5\n";

    public AICsvAnalysisServiceDelimiterTests()
    {
        _featureFlags.AiCategorization.Returns(true);
        // Simulate the configured AI returning a mapping for the pt-BR headers.
        _llm.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(
                """
                {
                  "date": {"column": "Data", "confidence": 0.95},
                  "amount": {"column": "Valor", "confidence": 0.95},
                  "description": {"column": "Descricao", "confidence": 0.9},
                  "type": {"column": "Tipo", "confidence": 0.8},
                  "balance": {"column": "Saldo", "confidence": 0.7}
                }
                """));

        _sut = new AICsvAnalysisService(_llm, _featureFlags, Substitute.For<ILogger<AICsvAnalysisService>>());
    }

    [Fact]
    public async Task AnalyzeCsvStructure_TabDelimited_SplitsIntoSeparateColumns()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TabDelimitedCsv));

        var result = await _sut.AnalyzeCsvStructureAsync(stream, "Checking", "BRL");

        result.Success.Should().BeTrue();
        // The critical regression: 5 distinct columns, not one merged "Data\tTipo\t..." column.
        result.AvailableColumns.Should().BeEquivalentTo("Data", "Tipo", "Descricao", "Valor", "Saldo");
    }

    [Fact]
    public async Task AnalyzeCsvStructure_TabDelimited_MapsAmountAndDateColumns()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TabDelimitedCsv));

        var result = await _sut.AnalyzeCsvStructureAsync(stream, "Checking", "BRL");

        result.SuggestedMappings.Should().ContainKey("amount");
        result.SuggestedMappings["amount"].CsvColumnName.Should().Be("Valor");
        result.SuggestedMappings.Should().ContainKey("date");
        result.SuggestedMappings["date"].CsvColumnName.Should().Be("Data");
    }
}
