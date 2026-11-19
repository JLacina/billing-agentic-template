using Billing.Domain.Invoices;

namespace Billing.Application;

public sealed record CreateInvoiceRequest(string CustomerId, string Currency);

public sealed record AddLineRequest(string Description, int Quantity, decimal UnitPrice, decimal TaxRate);

public sealed record RecordPaymentRequest(decimal Amount);

public sealed record InvoiceLineDto(string Description, int Quantity, decimal UnitPrice, decimal TaxRate, decimal Gross);

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
