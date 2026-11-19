using Billing.Domain.Common;

namespace Billing.Domain.Invoices;

/// <summary>Invoice lifecycle: Draft → Issued → PartiallyPaid → Paid. Only drafts can be edited.</summary>
public enum InvoiceStatus
{
    /// <summary>Being prepared; lines can be added.</summary>
    Draft,

    /// <summary>Finalised for payment; lines cannot be edited. Delivery to the customer is outside this operation.</summary>
    Issued,

    /// <summary>Some, but not all, of the total has been paid.</summary>
    PartiallyPaid,

    /// <summary>Fully paid; no further payments accepted.</summary>
    Paid,
}

/// <summary>
/// One immutable invoice line. Tax is calculated and rounded per line (see the rounding question in PLAN.md).
/// Create lines through <see cref="Invoice.AddLine"/>, which validates them.
/// </summary>
/// <param name="Description">What is being charged for.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="UnitPrice">Price per unit, in the invoice currency.</param>
/// <param name="TaxRate">Tax as a fraction, e.g. 0.20 for 20%.</param>
public sealed record InvoiceLine(string Description, int Quantity, Money UnitPrice, decimal TaxRate)
{
    /// <summary>Quantity × unit price, before tax.</summary>
    public Money Net => UnitPrice.Multiply(Quantity);

    /// <summary>Net × tax rate, rounded to 2 decimal places.</summary>
    public Money Tax => Net.Multiply(TaxRate);

    /// <summary>Net + tax: the amount the customer pays for this line.</summary>
    public Money Gross => Net.Add(Tax);
}
