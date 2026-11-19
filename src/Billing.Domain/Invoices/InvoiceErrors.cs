using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>Stable error codes are part of the API contract. Don't rename them casually.</summary>
public static class InvoiceErrors
{
    // Validation: the request itself is malformed or outside the supported limits (400).
    public static readonly Error InvalidCustomer = new("invoice.invalid_customer", $"Customer id is required and must be at most {Invoice.MaxCustomerIdLength} characters.", ErrorType.Validation);
    public static readonly Error UnsupportedCurrency = new("invoice.unsupported_currency", $"Currency must be one of: {string.Join(", ", Currencies.Supported)}.", ErrorType.Validation);
    public static readonly Error InvalidDescription = new("invoice.invalid_description", $"Description is required and must be at most {Invoice.MaxDescriptionLength} characters.", ErrorType.Validation);
    public static readonly Error InvalidQuantity = new("invoice.invalid_quantity", $"Quantity must be between 1 and {Invoice.MaxQuantity:N0}.", ErrorType.Validation);
    public static readonly Error InvalidPrice = new("invoice.invalid_price", "Unit price cannot be negative.", ErrorType.Validation);
    public static readonly Error InvalidTaxRate = new("invoice.invalid_tax_rate", "Tax rate must be between 0 and 1.", ErrorType.Validation);
    public static readonly Error AmountTooLarge = new("invoice.amount_too_large", $"Amounts and invoice totals cannot exceed {Invoice.MaxAmount:N0}.", ErrorType.Validation);
    public static readonly Error InvalidPayment = new("invoice.invalid_payment", "Payment amount must be greater than zero.", ErrorType.Validation);
    public static readonly Error CurrencyMismatch = new("invoice.currency_mismatch", "Amount currency does not match the invoice currency.", ErrorType.Validation);

    // Business rules: valid input, but not allowed in the invoice's current state (422).
    public static readonly Error NotDraft = new("invoice.not_draft", "Only draft invoices can be changed.");
    public static readonly Error NoLines = new("invoice.no_lines", "An invoice needs at least one line before it can be issued.");
    public static readonly Error NotPayable = new("invoice.not_payable", "Payments can only be recorded against issued invoices.");
    public static readonly Error Overpayment = new("invoice.overpayment", "Payment exceeds the outstanding balance.");

    public static readonly Error NotFound = new("invoice.not_found", "Invoice not found.", ErrorType.NotFound);
    public static readonly Error ConcurrencyConflict = new("invoice.concurrency_conflict", "The invoice was changed by another request. Reload it and try again.", ErrorType.Conflict);
}
