using System.Diagnostics.CodeAnalysis;

namespace Billing.Domain.Common;

/// <summary>
/// Explicit supported-currency policy. Only codes listed here can be invoiced; adding a currency
/// is a deliberate product decision (tax, rounding and reporting must support it too).
/// </summary>
public static class Currencies
{
    public static IReadOnlySet<string> Supported { get; } =
        new HashSet<string>(["GBP", "EUR", "USD"], StringComparer.Ordinal);

    /// <summary>Trims and upper-cases the input; succeeds only for a supported ISO 4217 code.</summary>
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
