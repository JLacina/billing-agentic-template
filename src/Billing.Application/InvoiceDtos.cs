using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>Request body for <c>POST /api/invoices</c>: create a draft invoice.</summary>
/// <param name="CustomerId">The billing account to invoice; required.</param>
/// <param name="Currency">ISO 4217 code from the supported list (case-insensitive), e.g. <c>"GBP"</c>.</param>
public sealed record CreateInvoiceRequest(string CustomerId, string Currency);

/// <summary>Request body for <c>POST /api/invoices/{id}/lines</c>: add a line to a draft.</summary>
/// <param name="Description">What is being charged for; required.</param>
/// <param name="Quantity">Number of units; at least 1.</param>
/// <param name="UnitPrice">Price per unit in the invoice's currency; zero or more.</param>
/// <param name="TaxRate">Tax as a fraction, e.g. 0.20 for 20% VAT.</param>
public sealed record AddLineRequest(string Description, int Quantity, decimal UnitPrice, decimal TaxRate);

/// <summary>Request body for <c>POST /api/invoices/{id}/payments</c>: record money already received (not a card capture).</summary>
/// <param name="Amount">Amount received in the invoice's currency; greater than zero and not above the outstanding balance.</param>
public sealed record RecordPaymentRequest(decimal Amount);

/// <summary>One invoice line as returned by the API.</summary>
/// <param name="Description">What is being charged for.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="UnitPrice">Price per unit.</param>
/// <param name="TaxRate">Tax as a fraction.</param>
/// <param name="Gross">Line total including tax.</param>
public sealed record InvoiceLineDto(string Description, int Quantity, decimal UnitPrice, decimal TaxRate, decimal Gross);

/// <summary>
/// Invoice as returned by the API. Amounts are in <paramref name="Currency"/>, rounded to 2 decimal places.
/// Mirrored by <c>web/src/features/invoices/types.ts</c>; keep both in sync.
/// </summary>
/// <param name="Id">Invoice id.</param>
/// <param name="CustomerId">Billing account being invoiced.</param>
/// <param name="Currency">Currency of every amount on the invoice.</param>
/// <param name="Status">Draft, Issued, PartiallyPaid or Paid.</param>
/// <param name="IssuedOn">Issue date, or <c>null</c> for a draft.</param>
/// <param name="Total">Sum of all line gross amounts.</param>
/// <param name="AmountPaid">Sum of payments recorded so far.</param>
/// <param name="Outstanding">Total minus amount paid.</param>
/// <param name="Lines">The invoice lines.</param>
public sealed record InvoiceDto(
    Guid Id,
    string CustomerId,
    string Currency,
    string Status,
    DateOnly? IssuedOn,
    decimal Total,
    decimal AmountPaid,
    decimal Outstanding,
    IReadOnlyList<InvoiceLineDto> Lines)
{
    /// <summary>Maps the domain aggregate to the API contract.</summary>
    /// <param name="invoice">The invoice to convert.</param>
    /// <returns>A DTO with plain decimal amounts and the status as text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invoice"/> is <c>null</c>.</exception>
    public static InvoiceDto From(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return new InvoiceDto(
            invoice.Id,
            invoice.CustomerId,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.IssuedOn,
            invoice.Total.Amount,
            invoice.AmountPaid.Amount,
            invoice.Outstanding.Amount,
            [.. invoice.Lines.Select(l => new InvoiceLineDto(l.Description, l.Quantity, l.UnitPrice.Amount, l.TaxRate, l.Gross.Amount))]);
    }
}
