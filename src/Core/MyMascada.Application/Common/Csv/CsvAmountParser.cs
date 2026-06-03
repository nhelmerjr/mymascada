using System.Globalization;
using System.Text;

namespace MyMascada.Application.Common.Csv;

/// <summary>
/// Locale-aware parser for monetary amounts found in bank-statement CSV exports.
/// Handles both en-US (1,234.56 / -80.49) and pt-BR (1.234,56 / -80,49) conventions,
/// currency symbols, and parentheses-as-negative — without assuming a single culture.
/// </summary>
public static class CsvAmountParser
{
    /// <summary>
    /// Parses a raw amount string into a decimal. Returns 0 when the value is empty
    /// or cannot be interpreted. Never throws.
    /// </summary>
    public static decimal Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0m;

        var s = raw.Trim();
        var negative = false;

        // Accounting style: (80,49) means -80,49
        if (s.StartsWith('(') && s.EndsWith(')'))
        {
            negative = true;
            s = s[1..^1];
        }

        // Keep only digits, separators and signs; drop currency symbols, spaces, etc.
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsDigit(ch) || ch is '-' or '+' or '.' or ',')
                sb.Append(ch);
        }
        var cleaned = sb.ToString();
        if (cleaned.Length == 0)
            return 0m;

        if (cleaned.Contains('-'))
            negative = true;
        cleaned = cleaned.Replace("+", string.Empty).Replace("-", string.Empty);
        if (cleaned.Length == 0)
            return 0m;

        var lastComma = cleaned.LastIndexOf(',');
        var lastDot = cleaned.LastIndexOf('.');

        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            // The rightmost separator is the decimal one; the other groups thousands.
            normalized = lastComma > lastDot
                ? cleaned.Replace(".", string.Empty).Replace(',', '.') // pt-BR: 1.234,56
                : cleaned.Replace(",", string.Empty);                  // en-US: 1,234.56
        }
        else if (lastComma >= 0)
        {
            normalized = IsDecimalSeparator(cleaned, ',', lastComma)
                ? cleaned.Replace(',', '.')               // decimal comma: -80,49
                : cleaned.Replace(",", string.Empty);     // thousands grouping: 1,234,567
        }
        else if (lastDot >= 0)
        {
            normalized = IsDecimalSeparator(cleaned, '.', lastDot)
                ? cleaned                                 // decimal dot: -80.49
                : cleaned.Replace(".", string.Empty);     // thousands grouping: 1.234.567
        }
        else
        {
            normalized = cleaned;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            return negative ? -Math.Abs(value) : value;

        return 0m;
    }

    /// <summary>
    /// A single occurrence of <paramref name="sep"/> followed by 1–2 trailing digits is
    /// treated as a decimal separator (bank amounts use 2 decimals). Multiple occurrences,
    /// or 3+ trailing digits, indicate thousands grouping (e.g. 1.234.567 / 1,234,567).
    /// </summary>
    private static bool IsDecimalSeparator(string value, char sep, int lastIndex)
    {
        var isSingle = value.IndexOf(sep) == lastIndex;
        var trailingDigits = value.Length - lastIndex - 1;
        return isSingle && trailingDigits is >= 1 and <= 2;
    }
}
