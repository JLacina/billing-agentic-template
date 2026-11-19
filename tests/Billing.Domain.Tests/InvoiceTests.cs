using Billing.Domain.Common;
using Billing.Domain.Invoices;
using FluentAssertions;

namespace Billing.Domain.Tests;

public class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);

    private static Invoice Draft(string currency = "GBP") => Invoice.CreateDraft("cust-1", currency).Value;

    private static Invoice IssuedInvoiceFor(decimal unitPrice, decimal taxRate = 0.20m)
    {
        var invoice = Draft();
        invoice.AddLine("Season ticket", 1, Money.Of(unitPrice, "GBP"), taxRate);
        invoice.Issue(Today);
        return invoice;
    }

    [Fact]
    public void CreateDraft_StartsAsDraftWithZeroTotal()
    {
        var invoice = Draft();

        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.Total.Should().Be(Money.Zero("GBP"));
    }

    [Fact]
    public void CreateDraft_NormalisesCurrency() =>
        Invoice.CreateDraft("cust-1", " usd ").Value.Currency.Should().Be("USD");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("XYZ")]
    public void CreateDraft_InvalidCurrency_FailsWithValidationError(string? currency)
    {
        var result = Invoice.CreateDraft("cust-1", currency!);

        result.Error.Should().Be(InvoiceErrors.UnsupportedCurrency);
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_MissingCustomer_Fails(string? customerId) =>
        Invoice.CreateDraft(customerId!, "GBP").Error.Should().Be(InvoiceErrors.InvalidCustomer);

    [Fact]
    public void CreateDraft_CustomerIdTooLong_Fails() =>
        Invoice.CreateDraft(new string('c', Invoice.MaxCustomerIdLength + 1), "GBP")
            .Error.Should().Be(InvoiceErrors.InvalidCustomer);

    [Fact]
    public void Total_SumsGrossOfAllLinesIncludingTax()
    {
        var invoice = Draft();
        invoice.AddLine("Pie", 2, Money.Of(4.50m, "GBP"), 0.20m);   // 9.00 + 1.80
        invoice.AddLine("Programme", 1, Money.Of(3m, "GBP"), 0m);    // 3.00

        invoice.Total.Should().Be(Money.Of(13.80m, "GBP"));
    }

    [Theory]
    [InlineData(0, 1, 0.2, "invoice.invalid_quantity")]
    [InlineData(1_000_001, 1, 0.2, "invoice.invalid_quantity")]
    [InlineData(1, -1, 0.2, "invoice.invalid_price")]
    [InlineData(1, 1, 1.5, "invoice.invalid_tax_rate")]
    [InlineData(1, 1, -0.1, "invoice.invalid_tax_rate")]
    public void AddLine_InvalidInput_Fails(int quantity, decimal price, decimal taxRate, string expectedCode)
    {
        var invoice = Draft();

        var result = invoice.AddLine("x", quantity, Money.Of(price, "GBP"), taxRate);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        invoice.Lines.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLine_MissingDescription_Fails(string? description) =>
        Draft().AddLine(description!, 1, Money.Of(1m, "GBP"), 0m).Error.Should().Be(InvoiceErrors.InvalidDescription);

    [Fact]
    public void AddLine_DescriptionTooLong_Fails() =>
        Draft().AddLine(new string('d', Invoice.MaxDescriptionLength + 1), 1, Money.Of(1m, "GBP"), 0m)
            .Error.Should().Be(InvoiceErrors.InvalidDescription);

    // Regression: a line whose gross overflowed decimal used to be added first, leaving an invoice
    // whose Total threw OverflowException on every later read.
    [Fact]
    public void AddLine_PriceThatWouldOverflow_IsRejectedAndInvoiceStaysUsable()
    {
        var invoice = Draft();

        var result = invoice.AddLine("huge", 2, Money.Of(decimal.MaxValue, "GBP"), 0m);

        result.Error.Should().Be(InvoiceErrors.AmountTooLarge);
        invoice.Lines.Should().BeEmpty();
        invoice.Total.Should().Be(Money.Zero("GBP"));
    }

    [Fact]
    public void AddLine_UnitPriceAboveLimit_Fails() =>
        Draft().AddLine("x", 1, Money.Of(Invoice.MaxAmount + 0.01m, "GBP"), 0m).Error.Should().Be(InvoiceErrors.AmountTooLarge);

    [Fact]
    public void AddLine_LineGrossAboveLimit_Fails() =>
        Draft().AddLine("x", 2, Money.Of(Invoice.MaxAmount / 2, "GBP"), 0.01m).Error.Should().Be(InvoiceErrors.AmountTooLarge);

    [Fact]
    public void AddLine_ResultingTotalAboveLimit_FailsAndKeepsExistingLines()
    {
        var invoice = Draft();
        invoice.AddLine("first", 1, Money.Of(Invoice.MaxAmount, "GBP"), 0m).IsSuccess.Should().BeTrue();

        var result = invoice.AddLine("second", 1, Money.Of(0.01m, "GBP"), 0m);

        result.Error.Should().Be(InvoiceErrors.AmountTooLarge);
        invoice.Lines.Should().HaveCount(1);
        invoice.Total.Should().Be(Money.Of(Invoice.MaxAmount, "GBP"));
    }

    [Fact]
    public void AddLine_WrongCurrency_Fails() =>
        Draft().AddLine("x", 1, Money.Of(1m, "EUR"), 0m).Error.Should().Be(InvoiceErrors.CurrencyMismatch);

    [Fact]
    public void Issue_WithoutLines_Fails() =>
        Draft().Issue(Today).Error.Should().Be(InvoiceErrors.NoLines);

    [Fact]
    public void Issue_SetsStatusAndDate()
    {
        var invoice = IssuedInvoiceFor(10m);

        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.IssuedOn.Should().Be(Today);
    }

    [Fact]
    public void IssuedInvoice_CannotBeEdited() =>
        IssuedInvoiceFor(10m).AddLine("late", 1, Money.Of(1m, "GBP"), 0m).Error.Should().Be(InvoiceErrors.NotDraft);

    [Fact]
    public void RecordPayment_OnDraft_Fails() =>
        Draft().RecordPayment(Money.Of(1m, "GBP")).Error.Should().Be(InvoiceErrors.NotPayable);

    [Fact]
    public void RecordPayment_Partial_ThenFull_MovesThroughStatuses()
    {
        var invoice = IssuedInvoiceFor(100m); // total 120.00

        invoice.RecordPayment(Money.Of(20m, "GBP")).IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        invoice.Outstanding.Should().Be(Money.Of(100m, "GBP"));

        invoice.RecordPayment(Money.Of(100m, "GBP")).IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Outstanding.Should().Be(Money.Zero("GBP"));
    }

    [Fact]
    public void RecordPayment_MoreThanOutstanding_FailsAndChangesNothing()
    {
        var invoice = IssuedInvoiceFor(100m);

        invoice.RecordPayment(Money.Of(120.01m, "GBP")).Error.Should().Be(InvoiceErrors.Overpayment);
        invoice.AmountPaid.Should().Be(Money.Zero("GBP"));
        invoice.Status.Should().Be(InvoiceStatus.Issued);
    }

    [Fact]
    public void RecordPayment_HugeAmount_IsOverpaymentNotOverflow() =>
        IssuedInvoiceFor(10m).RecordPayment(Money.Of(decimal.MaxValue, "GBP")).Error.Should().Be(InvoiceErrors.Overpayment);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RecordPayment_NonPositive_Fails(decimal amount) =>
        IssuedInvoiceFor(10m).RecordPayment(Money.Of(amount, "GBP")).Error.Should().Be(InvoiceErrors.InvalidPayment);

    [Fact]
    public void PaidInvoice_RejectsFurtherPayments()
    {
        var invoice = IssuedInvoiceFor(10m, taxRate: 0m);
        invoice.RecordPayment(Money.Of(10m, "GBP"));

        invoice.RecordPayment(Money.Of(1m, "GBP")).Error.Should().Be(InvoiceErrors.NotPayable);
    }

    [Fact]
    public void Snapshot_RoundTrip_RestoresIdenticalState()
    {
        var original = IssuedInvoiceFor(100m);
        original.RecordPayment(Money.Of(20m, "GBP"));

        var restored = Invoice.FromSnapshot(original.ToSnapshot());

        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void Snapshot_IsIndependentOfLaterChanges()
    {
        var invoice = Draft();
        var snapshot = invoice.ToSnapshot();

        invoice.AddLine("after", 1, Money.Of(1m, "GBP"), 0m);

        snapshot.Lines.Should().BeEmpty();
    }
}
