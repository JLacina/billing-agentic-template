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

    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Zero(string currency) => Of(0m, currency);

    /// <summary>
    /// Creates an amount. Throws for an unsupported currency: callers handling user input must
    /// validate with <see cref="Currencies.TryNormalize"/> first and return a validation error.
    /// </summary>
    public static Money Of(decimal amount, string currency)
    {
        if (!Currencies.TryNormalize(currency, out var code))
        {
            throw new ArgumentException(
                $"Currency must be one of: {string.Join(", ", Currencies.Supported)}.", nameof(currency));
        }

        return new Money(decimal.Round(amount, 2, MidpointRounding.ToEven), code);
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return Of(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => Of(Amount * factor, Currency);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");
}
