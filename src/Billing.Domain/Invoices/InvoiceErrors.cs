using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>
/// Every expected failure for invoices. The <see cref="Error.Code"/> values are part of the API contract
/// (clients branch on them), so don't rename them casually. The <see cref="Error.Type"/> decides the HTTP status.
/// </summary>
public static class InvoiceErrors
{
    // ---- Validation: the request itself is malformed or outside the supported limits (HTTP 400). ----

    /// <summary>Customer id is empty, whitespace, or longer than <see cref="Invoice.MaxCustomerIdLength"/>.</summary>
    public static readonly Error InvalidCustomer = new("invoice.invalid_customer", $"Customer id is required and must be at most {Invoice.MaxCustomerIdLength} characters.", ErrorType.Validation);

    /// <summary>Currency is missing, malformed (e.g. <c>"123"</c>) or not in <see cref="Currencies.Supported"/>.</summary>
    public static readonly Error UnsupportedCurrency = new("invoice.unsupported_currency", $"Currency must be one of: {string.Join(", ", Currencies.Supported)}.", ErrorType.Validation);

    /// <summary>Line description is empty, whitespace, or longer than <see cref="Invoice.MaxDescriptionLength"/>.</summary>
    public static readonly Error InvalidDescription = new("invoice.invalid_description", $"Description is required and must be at most {Invoice.MaxDescriptionLength} characters.", ErrorType.Validation);

    /// <summary>Line quantity is zero, negative, or above <see cref="Invoice.MaxQuantity"/>.</summary>
    public static readonly Error InvalidQuantity = new("invoice.invalid_quantity", $"Quantity must be between 1 and {Invoice.MaxQuantity:N0}.", ErrorType.Validation);

    /// <summary>Line unit price is negative.</summary>
    public static readonly Error InvalidPrice = new("invoice.invalid_price", "Unit price cannot be negative.", ErrorType.Validation);

    /// <summary>Tax rate is outside 0–1 (it is a fraction: 0.20 means 20%).</summary>
    public static readonly Error InvalidTaxRate = new("invoice.invalid_tax_rate", "Tax rate must be between 0 and 1.", ErrorType.Validation);

    /// <summary>
    /// A unit price, line total or resulting invoice total would exceed <see cref="Invoice.MaxAmount"/>.
    /// Protects against decimal overflow corrupting an invoice.
    /// </summary>
    public static readonly Error AmountTooLarge = new("invoice.amount_too_large", $"Amounts and invoice totals cannot exceed {Invoice.MaxAmount:N0}.", ErrorType.Validation);

    /// <summary>Payment amount is zero or negative.</summary>
    public static readonly Error InvalidPayment = new("invoice.invalid_payment", "Payment amount must be greater than zero.", ErrorType.Validation);

    /// <summary>A price or payment is in a different currency from the invoice.</summary>
    public static readonly Error CurrencyMismatch = new("invoice.currency_mismatch", "Amount currency does not match the invoice currency.", ErrorType.Validation);

    // ---- Business rules: valid input, but not allowed in the invoice's current state (HTTP 422). ----

    /// <summary>Tried to add a line to, or re-issue, an invoice that is no longer a draft.</summary>
    public static readonly Error NotDraft = new("invoice.not_draft", "Only draft invoices can be changed.");

    /// <summary>Tried to issue an invoice with no lines.</summary>
    public static readonly Error NoLines = new("invoice.no_lines", "An invoice needs at least one line before it can be issued.");

    /// <summary>Tried to pay a draft or an already fully paid invoice.</summary>
    public static readonly Error NotPayable = new("invoice.not_payable", "Payments can only be recorded against issued invoices.");

    /// <summary>Payment is larger than the outstanding balance.</summary>
    public static readonly Error Overpayment = new("invoice.overpayment", "Payment exceeds the outstanding balance.");

    // ---- Other ----

    /// <summary>No invoice exists with the given id (HTTP 404).</summary>
    public static readonly Error NotFound = new("invoice.not_found", "Invoice not found.", ErrorType.NotFound);

    /// <summary>
    /// Another request saved the invoice after this one loaded it (HTTP 409). Nothing was saved;
    /// the client should reload and retry.
    /// </summary>
    public static readonly Error ConcurrencyConflict = new("invoice.concurrency_conflict", "The invoice was changed by another request. Reload it and try again.", ErrorType.Conflict);
}
