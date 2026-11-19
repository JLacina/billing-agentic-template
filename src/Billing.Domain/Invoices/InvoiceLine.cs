using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
}

public sealed record InvoiceLine(string Description, int Quantity, Money UnitPrice, decimal TaxRate)
{
    public Money Net => UnitPrice.Multiply(Quantity);
    public Money Tax => Net.Multiply(TaxRate);
    public Money Gross => Net.Add(Tax);
}
