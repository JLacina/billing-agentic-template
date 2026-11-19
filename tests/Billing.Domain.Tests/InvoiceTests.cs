using Billing.Domain.Common;
using Billing.Domain.Invoices;
using FluentAssertions;

namespace Billing.Domain.Tests;

public class InvoiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);

    private static Invoice IssuedInvoiceFor(decimal unitPrice, decimal taxRate = 0.20m)
    {
        var invoice = Invoice.CreateDraft("cust-1", "GBP");
        invoice.AddLine("Season ticket", 1, Money.Of(unitPrice, "GBP"), taxRate);
        invoice.Issue(Today);
        return invoice;
    }

    [Fact]
    public void CreateDraft_StartsAsDraftWithZeroTotal()
    {
        var invoice = Invoice.CreateDraft("cust-1", "GBP");

        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.Total.Should().Be(Money.Zero("GBP"));
    }

    [Fact]
    public void Total_SumsGrossOfAllLinesIncludingTax()
    {
        var invoice = Invoice.CreateDraft("cust-1", "GBP");
        invoice.AddLine("Pie", 2, Money.Of(4.50m, "GBP"), 0.20m);   // 9.00 + 1.80
        invoice.AddLine("Programme", 1, Money.Of(3m, "GBP"), 0m);    // 3.00

        invoice.Total.Should().Be(Money.Of(13.80m, "GBP"));
    }

    [Theory]
    [InlineData(0, 1, 0.2, "invoice.invalid_quantity")]
    [InlineData(1, -1, 0.2, "invoice.invalid_price")]
    [InlineData(1, 1, 1.5, "invoice.invalid_tax_rate")]
    public void AddLine_InvalidInput_Fails(int quantity, decimal price, decimal taxRate, string expectedCode)
    {
        var invoice = Invoice.CreateDraft("cust-1", "GBP");

        var result = invoice.AddLine("x", quantity, Money.Of(price, "GBP"), taxRate);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
        invoice.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AddLine_WrongCurrency_Fails() =>
        Invoice.CreateDraft("cust-1", "GBP")
            .AddLine("x", 1, Money.Of(1m, "EUR"), 0m)
            .Error.Should().Be(InvoiceErrors.CurrencyMismatch);

    [Fact]
    public void Issue_WithoutLines_Fails() =>
        Invoice.CreateDraft("cust-1", "GBP").Issue(Today).Error.Should().Be(InvoiceErrors.NoLines);

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
        Invoice.CreateDraft("cust-1", "GBP").RecordPayment(Money.Of(1m, "GBP")).Error.Should().Be(InvoiceErrors.NotPayable);

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
}
