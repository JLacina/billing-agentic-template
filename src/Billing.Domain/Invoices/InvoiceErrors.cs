using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

public static class InvoiceErrors
{
    public static readonly Error NotDraft = new("invoice.not_draft", "Only draft invoices can be changed.");
    public static readonly Error NoLines = new("invoice.no_lines", "An invoice needs at least one line before it can be issued.");
    public static readonly Error NotPayable = new("invoice.not_payable", "Payments can only be recorded against issued invoices.");
    public static readonly Error Overpayment = new("invoice.overpayment", "Payment exceeds the outstanding balance.");
    public static readonly Error InvalidQuantity = new("invoice.invalid_quantity", "Quantity must be greater than zero.");
    public static readonly Error InvalidPrice = new("invoice.invalid_price", "Unit price cannot be negative.");
    public static readonly Error InvalidTaxRate = new("invoice.invalid_tax_rate", "Tax rate must be between 0 and 1.");
    public static readonly Error InvalidPayment = new("invoice.invalid_payment", "Payment amount must be greater than zero.");
    public static readonly Error CurrencyMismatch = new("invoice.currency_mismatch", "Amount currency does not match the invoice currency.");
    public static readonly Error NotFound = new("invoice.not_found", "Invoice not found.");
}
