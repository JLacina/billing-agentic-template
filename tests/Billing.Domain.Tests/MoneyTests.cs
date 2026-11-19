using Billing.Domain.Common;
using FluentAssertions;

namespace Billing.Domain.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(10.125, 10.12)] // banker's rounding: midpoint goes to even
    [InlineData(10.135, 10.14)]
    [InlineData(10.1, 10.10)]
    public void Of_RoundsToTwoDecimalsWithBankersRounding(decimal input, decimal expected) =>
        Money.Of(input, "GBP").Amount.Should().Be(expected);

    [Theory]
    [InlineData("gbp", "GBP")]
    [InlineData(" eur ", "EUR")]
    public void Of_NormalisesCurrency(string input, string expected) =>
        Money.Of(1m, input).Currency.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GB")]
    [InlineData("POUND")]
    [InlineData("123")]
    [InlineData("XYZ")] // well-formed but not in the supported list
    public void Of_RejectsInvalidOrUnsupportedCurrency(string currency)
    {
        var act = () => Money.Of(1m, currency);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Of_RejectsNullCurrency()
    {
        var act = () => Money.Of(1m, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        var act = () => Money.Of(1m, "GBP").Add(Money.Of(1m, "EUR"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Money_IsAValueObject_EqualByValue() =>
        Money.Of(5m, "GBP").Should().Be(Money.Of(5.00m, "gbp"));
}

public class CurrenciesTests
{
    [Theory]
    [InlineData("GBP", "GBP")]
    [InlineData("usd", "USD")]
    [InlineData("  eur  ", "EUR")]
    public void TryNormalize_SupportedCode_ReturnsUpperCaseCode(string input, string expected)
    {
        Currencies.TryNormalize(input, out var code).Should().BeTrue();
        code.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("GB")]
    [InlineData("XYZ")]
    [InlineData("G B")]
    public void TryNormalize_InvalidOrUnsupported_ReturnsFalse(string? input) =>
        Currencies.TryNormalize(input, out _).Should().BeFalse();
}
