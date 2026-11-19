using System.Diagnostics.CodeAnalysis;

namespace Billing.Domain.Common;

/// <summary>
/// Explicit supported-currency policy. Only codes listed here can be invoiced; adding a currency
/// is a deliberate product decision (tax, rounding and reporting must support it too).
/// </summary>
public static class Currencies
{
    /// <summary>Upper-case ISO 4217 codes that can be invoiced.</summary>
    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string>(["GBP", "EUR", "USD"], StringComparer.Ordinal);

    /// <summary>
    /// Validates and normalises a currency code from user input: trims whitespace, upper-cases it,
    /// and accepts it only if it is in <see cref="Supported"/>.
    /// </summary>
    /// <param name="value">Raw input, e.g. <c>" usd "</c>. May be <c>null</c>.</param>
    /// <param name="code">When this method returns <c>true</c>, the normalised code (e.g. <c>"USD"</c>); otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if <paramref name="value"/> is a supported currency; <c>false</c> for null, empty, malformed or unsupported codes.</returns>
    public static bool TryNormalize(string? value, [NotNullWhen(true)] out string? code)
    {
        var candidate = value?.Trim().ToUpperInvariant();
        if (candidate is not null && Supported.Contains(candidate))
        {
            code = candidate;
            return true;
        }

        code = null;
        return false;
    }
}
