using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>Immutable copy of an invoice's state, used by persistence adapters.</summary>
public sealed record InvoiceSnapshot(
    Guid Id,
    string CustomerId,
    string Currency,
    InvoiceStatus Status,
    DateOnly? IssuedOn,
    decimal AmountPaid,
    IReadOnlyList<InvoiceLine> Lines);

/// <summary>
/// Aggregate root. Every state change goes through a method that enforces the business rules,
/// and every check runs before any state changes, so a rejected operation leaves the invoice untouched.
/// </summary>
public sealed class Invoice
{
    /// <summary>Upper bound for unit prices, line totals and invoice totals. Keeps all arithmetic far from decimal overflow.</summary>
    public const decimal MaxAmount = 1_000_000_000m;
    public const int MaxQuantity = 1_000_000;
    public const int MaxDescriptionLength = 200;
    public const int MaxCustomerIdLength = 64;

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

    public static Result<Invoice> CreateDraft(string customerId, string currency)
    {
        if (string.IsNullOrWhiteSpace(customerId) || customerId.Length > MaxCustomerIdLength)
        {
            return Result<Invoice>.Failure(InvoiceErrors.InvalidCustomer);
        }
        if (!Currencies.TryNormalize(currency, out var code))
        {
            return Result<Invoice>.Failure(InvoiceErrors.UnsupportedCurrency);
        }

        return Result<Invoice>.Success(new Invoice(Guid.NewGuid(), customerId.Trim(), code));
    }

    public Result AddLine(string description, int quantity, Money unitPrice, decimal taxRate)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (Status != InvoiceStatus.Draft) { return Result.Failure(InvoiceErrors.NotDraft); }
        if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
        {
            return Result.Failure(InvoiceErrors.InvalidDescription);
        }
        if (quantity is <= 0 or > MaxQuantity) { return Result.Failure(InvoiceErrors.InvalidQuantity); }
        if (unitPrice.Amount < 0) { return Result.Failure(InvoiceErrors.InvalidPrice); }
        if (unitPrice.Amount > MaxAmount) { return Result.Failure(InvoiceErrors.AmountTooLarge); }
        if (taxRate is < 0 or > 1) { return Result.Failure(InvoiceErrors.InvalidTaxRate); }
        if (unitPrice.Currency != Currency) { return Result.Failure(InvoiceErrors.CurrencyMismatch); }

        // Safe: the bounds above cap the gross at 1e9 * 1e6 * 2, far below decimal.MaxValue.
        var line = new InvoiceLine(description.Trim(), quantity, unitPrice, taxRate);
        if (line.Gross.Amount > MaxAmount || Total.Amount + line.Gross.Amount > MaxAmount)
        {
            return Result.Failure(InvoiceErrors.AmountTooLarge);
        }

        _lines.Add(line);
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

    public InvoiceSnapshot ToSnapshot() =>
        new(Id, CustomerId, Currency, Status, IssuedOn, AmountPaid.Amount, [.. _lines]);

    /// <summary>
    /// Rebuilds an invoice from persisted state. For persistence adapters only: it trusts the snapshot
    /// because it was produced by <see cref="ToSnapshot"/> from a valid invoice.
    /// </summary>
    public static Invoice FromSnapshot(InvoiceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var invoice = new Invoice(snapshot.Id, snapshot.CustomerId, snapshot.Currency)
        {
            Status = snapshot.Status,
            IssuedOn = snapshot.IssuedOn,
            AmountPaid = Money.Of(snapshot.AmountPaid, snapshot.Currency),
        };
        invoice._lines.AddRange(snapshot.Lines);
        return invoice;
    }
}
