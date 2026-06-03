using FluentAssertions;
using MyMascada.Application.Common.Csv;

namespace MyMascada.Tests.Unit.Common;

public class CsvAmountParserTests
{
    [Theory]
    // pt-BR convention (decimal comma) — values taken from a real Banco Inter statement
    [InlineData("-80,49", -80.49)]
    [InlineData("-44,9", -44.9)]
    [InlineData("847,2", 847.2)]
    [InlineData("-0,5", -0.5)]
    [InlineData("1,86", 1.86)]
    [InlineData("188,9", 188.9)]
    [InlineData("-15", -15)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1.234.567,89", 1234567.89)]
    public void Parse_PtBrConvention_ReturnsExpected(string raw, double expected)
    {
        CsvAmountParser.Parse(raw).Should().Be((decimal)expected);
    }

    [Theory]
    // en-US convention (decimal dot)
    [InlineData("-80.49", -80.49)]
    [InlineData("5.25", 5.25)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("1,234,567.89", 1234567.89)]
    [InlineData("-15", -15)]
    public void Parse_EnUsConvention_ReturnsExpected(string raw, double expected)
    {
        CsvAmountParser.Parse(raw).Should().Be((decimal)expected);
    }

    [Theory]
    // Currency symbols and accounting-style negatives
    [InlineData("$5.25", 5.25)]
    [InlineData("R$ -80,49", -80.49)]
    [InlineData("€1.234,56", 1234.56)]
    [InlineData("(80,49)", -80.49)]
    [InlineData("(1,234.56)", -1234.56)]
    public void Parse_SymbolsAndParentheses_ReturnsExpected(string raw, double expected)
    {
        CsvAmountParser.Parse(raw).Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("0,00")]
    public void Parse_EmptyOrZeroOrInvalid_ReturnsZero(string? raw)
    {
        CsvAmountParser.Parse(raw).Should().Be(0m);
    }

    [Theory]
    // Thousands grouping without decimals must not be mistaken for a decimal separator
    [InlineData("1.234", 1234)]
    [InlineData("1,234", 1234)]
    [InlineData("1.234.567", 1234567)]
    public void Parse_ThousandsGroupingWithoutDecimals_ReturnsExpected(string raw, double expected)
    {
        CsvAmountParser.Parse(raw).Should().Be((decimal)expected);
    }
}
