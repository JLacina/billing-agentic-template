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

    [Fact]
    public void Of_NormalisesCurrencyToUpperCase() =>
        Money.Of(1m, "gbp").Currency.Should().Be("GBP");

    [Theory]
    [InlineData("")]
    [InlineData("GB")]
    [InlineData("POUND")]
    public void Of_RejectsInvalidCurrency(string currency)
    {
        var act = () => Money.Of(1m, currency);
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
