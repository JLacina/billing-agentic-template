using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>Detached copy of an invoice's state, used by persistence adapters.</summary>
/// <param name="Id">Invoice id.</param>
/// <param name="CustomerId">Billing account being invoiced.</param>
/// <param name="Currency">Normalised currency code.</param>
/// <param name="Status">Lifecycle status at the time of the snapshot.</param>
/// <param name="IssuedOn">Issue date, or <c>null</c> for a draft.</param>
/// <param name="AmountPaid">Total paid so far, in <paramref name="Currency"/>.</param>
/// <param name="Lines">Copy of the invoice lines (lines are immutable records).</param>
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

    /// <summary>Largest allowed quantity on one line.</summary>
    public const int MaxQuantity = 1_000_000;

    /// <summary>Maximum length of a line description.</summary>
    public const int MaxDescriptionLength = 200;

    /// <summary>Maximum length of a customer id.</summary>
    public const int MaxCustomerIdLength = 64;

    private readonly List<InvoiceLine> _lines = [];

    private Invoice(Guid id, string customerId, string currency)
    {
        Id = id;
        CustomerId = customerId;
        Currency = currency;
        AmountPaid = Money.Zero(currency);
    }

    /// <summary>Unique id, generated when the draft is created.</summary>
    public Guid Id { get; }

    /// <summary>The billing account being invoiced (trimmed).</summary>
    public string CustomerId { get; }

    /// <summary>Currency for every amount on this invoice; fixed at creation.</summary>
    public string Currency { get; }

    /// <summary>Where the invoice is in its lifecycle. Changes only through <see cref="Issue"/> and <see cref="RecordPayment"/>.</summary>
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>Date the invoice was issued; <c>null</c> while it is a draft.</summary>
    public DateOnly? IssuedOn { get; private set; }

    /// <summary>Sum of all payments recorded so far.</summary>
    public Money AmountPaid { get; private set; }

    /// <summary>Read-only view of the lines. Add lines with <see cref="AddLine"/> so the rules are enforced.</summary>
    public IReadOnlyList<InvoiceLine> Lines => _lines.AsReadOnly();

    /// <summary>Sum of all line gross amounts (tax included).</summary>
    public Money Total => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum.Add(line.Gross));

    /// <summary>Total minus payments recorded so far. Never negative, because overpayment is rejected.</summary>
    public Money Outstanding => Total.Subtract(AmountPaid);

    /// <summary>Creates an empty draft invoice with a new id.</summary>
    /// <param name="customerId">The billing account; required, at most <see cref="MaxCustomerIdLength"/> characters, trimmed.</param>
    /// <param name="currency">Currency for the whole invoice; normalised (trimmed, upper-case) and must be in <see cref="Currencies.Supported"/>.</param>
    /// <returns>The new draft, or <see cref="InvoiceErrors.InvalidCustomer"/> / <see cref="InvoiceErrors.UnsupportedCurrency"/>.</returns>
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

    /// <summary>
    /// Adds a line to a draft. Validates description, quantity, price, tax rate, currency and the
    /// <see cref="MaxAmount"/> limits before changing anything, so a rejected line leaves the invoice untouched.
    /// </summary>
    /// <param name="description">What is being charged for; required, at most <see cref="MaxDescriptionLength"/> characters, trimmed.</param>
    /// <param name="quantity">Number of units, 1 to <see cref="MaxQuantity"/>.</param>
    /// <param name="unitPrice">Price per unit; zero or more, at most <see cref="MaxAmount"/>, in the invoice currency.</param>
    /// <param name="taxRate">Tax as a fraction between 0 and 1, e.g. 0.20 for 20% VAT.</param>
    /// <returns>
    /// Success; or <see cref="InvoiceErrors.NotDraft"/>, <see cref="InvoiceErrors.InvalidDescription"/>,
    /// <see cref="InvoiceErrors.InvalidQuantity"/>, <see cref="InvoiceErrors.InvalidPrice"/>,
    /// <see cref="InvoiceErrors.InvalidTaxRate"/>, <see cref="InvoiceErrors.CurrencyMismatch"/> or
    /// <see cref="InvoiceErrors.AmountTooLarge"/> (line or resulting total above <see cref="MaxAmount"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="unitPrice"/> is <c>null</c>.</exception>
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

    /// <summary>Issues a draft that has at least one line. After this, lines can't change and payments are accepted.</summary>
    /// <param name="issuedOn">The issue date, normally today from the application's clock.</param>
    /// <returns>Success; or <see cref="InvoiceErrors.NotDraft"/> if already issued, <see cref="InvoiceErrors.NoLines"/> if empty.</returns>
    public Result Issue(DateOnly issuedOn)
    {
        if (Status != InvoiceStatus.Draft) { return Result.Failure(InvoiceErrors.NotDraft); }
        if (_lines.Count == 0) { return Result.Failure(InvoiceErrors.NoLines); }

        Status = InvoiceStatus.Issued;
        IssuedOn = issuedOn;
        return Result.Success();
    }

    /// <summary>
    /// Records a payment against an issued invoice and moves it to PartiallyPaid or Paid.
    /// Rejects zero or negative amounts, a different currency, and anything above <see cref="Outstanding"/>.
    /// </summary>
    /// <param name="amount">Money already received, in the invoice currency.</param>
    /// <returns>
    /// Success; or <see cref="InvoiceErrors.NotPayable"/> (draft or already paid), <see cref="InvoiceErrors.CurrencyMismatch"/>,
    /// <see cref="InvoiceErrors.InvalidPayment"/> (zero or negative) or <see cref="InvoiceErrors.Overpayment"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="amount"/> is <c>null</c>.</exception>
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

    /// <summary>Copies the current state into a detached snapshot for persistence.</summary>
    /// <returns>A snapshot with its own copy of the lines, so later changes to this invoice do not affect it.</returns>
    public InvoiceSnapshot ToSnapshot() =>
        new(Id, CustomerId, Currency, Status, IssuedOn, AmountPaid.Amount, [.. _lines]);

    /// <summary>
    /// Rebuilds an invoice from persisted state. For persistence adapters only: it trusts the snapshot
    /// because it was produced by <see cref="ToSnapshot"/> from a valid invoice.
    /// </summary>
    /// <param name="snapshot">State previously produced by <see cref="ToSnapshot"/>.</param>
    /// <returns>A new invoice instance with exactly that state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is <c>null</c>.</exception>
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
