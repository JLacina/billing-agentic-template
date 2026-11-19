namespace Billing.Domain.Common;

/// <summary>
/// Immutable amount in a single currency. Never use double/float for money.
/// Rounded to 2 decimal places with banker's rounding (MidpointRounding.ToEven).
/// </summary>
public sealed record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>The amount, always rounded to 2 decimal places. Can be negative only as an intermediate result.</summary>
    public decimal Amount { get; }

    /// <summary>Upper-case ISO 4217 code from <see cref="Currencies.Supported"/>.</summary>
    public string Currency { get; }

    /// <summary>Creates a zero amount.</summary>
    /// <param name="currency">A supported currency code (case-insensitive).</param>
    /// <returns>0.00 in <paramref name="currency"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="currency"/> is not supported.</exception>
    public static Money Zero(string currency) => Of(0m, currency);

    /// <summary>
    /// Creates an amount, rounded to 2 decimal places with banker's rounding.
    /// </summary>
    /// <param name="amount">The amount; e.g. 10.125 becomes 10.12.</param>
    /// <param name="currency">A supported currency code (case-insensitive, surrounding spaces ignored).</param>
    /// <returns>The rounded amount in the normalised currency.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="currency"/> is not supported. Callers handling user input must validate with
    /// <see cref="Currencies.TryNormalize"/> first and return a validation error instead.
    /// </exception>
    public static Money Of(decimal amount, string currency)
    {
        if (!Currencies.TryNormalize(currency, out var code))
        {
            throw new ArgumentException(
                $"Currency must be one of: {string.Join(", ", Currencies.Supported)}.", nameof(currency));
        }

        return new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), code);
    }

    /// <summary>Adds another amount in the same currency.</summary>
    /// <param name="other">The amount to add.</param>
    /// <returns>A new amount: this + <paramref name="other"/>.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount + other.Amount, Currency);
    }

    /// <summary>Subtracts another amount in the same currency.</summary>
    /// <param name="other">The amount to subtract.</param>
    /// <returns>A new amount: this − <paramref name="other"/>. May be negative.</returns>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount - other.Amount, Currency);
    }

    /// <summary>Multiplies by a factor such as a quantity or a tax rate.</summary>
    /// <param name="factor">The multiplier, e.g. 3 for a quantity or 0.20 for 20% tax.</param>
    /// <returns>A new amount, rounded to 2 decimal places, in the same currency.</returns>
    public Money Multiply(decimal factor) => Of(Amount * factor, Currency);

    /// <summary>
    /// Guards arithmetic: adding GBP to EUR is a programming error, not a business case, so it throws
    /// instead of returning a <see cref="Result"/>. Business-level mismatches are caught earlier as validation errors.
    /// </summary>
    /// <param name="other">The amount being combined with this one.</param>
    /// <exception cref="InvalidOperationException">The currencies differ.</exception>
    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }

    /// <summary>Formats the amount for logs and test messages (not for UI display).</summary>
    /// <returns>Culture-independent text such as <c>"12.50 GBP"</c>.</returns>
    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");
}
