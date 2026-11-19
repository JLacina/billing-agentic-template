using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>
/// Aggregate root. Every state change goes through a method that enforces the business rules,
/// so nothing outside this class can put an invoice into an invalid state.
/// </summary>
public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice(Guid id, string customerId, string currency)
    {
        Id = id;
        CustomerId = customerId;
        Currency = currency;
        AmountPaid = Money.Zero(currency);
    }

    public Guid Id { get; }
    public string CustomerId { get; }
    public string Currency { get; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public DateOnly? IssuedOn { get; private set; }
    public Money AmountPaid { get; private set; }
    public IReadOnlyList<InvoiceLine> Lines => _lines.AsReadOnly();

    public Money Total => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum.Add(line.Gross));
    public Money Outstanding => Total.Subtract(AmountPaid);

    public static Invoice CreateDraft(string customerId, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        return new Invoice(Guid.NewGuid(), customerId, Money.Zero(currency).Currency);
    }

    public Result AddLine(string description, int quantity, Money unitPrice, decimal taxRate)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (Status != InvoiceStatus.Draft) { return Result.Failure(InvoiceErrors.NotDraft); }
        if (quantity <= 0) { return Result.Failure(InvoiceErrors.InvalidQuantity); }
        if (unitPrice.Amount < 0) { return Result.Failure(InvoiceErrors.InvalidPrice); }
        if (taxRate is < 0 or > 1) { return Result.Failure(InvoiceErrors.InvalidTaxRate); }
        if (unitPrice.Currency != Currency) { return Result.Failure(InvoiceErrors.CurrencyMismatch); }

        _lines.Add(new InvoiceLine(description, quantity, unitPrice, taxRate));
        return Result.Success();
    }

    public Result Issue(DateOnly issuedOn)
    {
        if (Status != InvoiceStatus.Draft) { return Result.Failure(InvoiceErrors.NotDraft); }
        if (_lines.Count == 0) { return Result.Failure(InvoiceErrors.NoLines); }

        Status = InvoiceStatus.Issued;
        IssuedOn = issuedOn;
        return Result.Success();
    }

    public Result RecordPayment(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid))
        {
            return Result.Failure(InvoiceErrors.NotPayable);
        }
        if (amount.Currency != Currency) { return Result.Failure(InvoiceErrors.CurrencyMismatch); }
        if (amount.Amount <= 0) { return Result.Failure(InvoiceErrors.InvalidPayment); }
        if (amount.Amount > Outstanding.Amount) { return Result.Failure(InvoiceErrors.Overpayment); }

        AmountPaid = AmountPaid.Add(amount);
        Status = Outstanding.Amount == 0 ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        return Result.Success();
    }
}
