using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>Persistence port. Implemented in Billing.Infrastructure (Dependency Inversion).</summary>
public interface IInvoiceRepository
{
    /// <summary>Returns a copy the caller owns; changes are invisible to others until saved.</summary>
    Task<Invoice?> GetAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Saves with optimistic concurrency. Returns <c>false</c> (and saves nothing) if the invoice was
    /// changed by someone else after this instance was loaded.
    /// </summary>
    Task<bool> SaveAsync(Invoice invoice, CancellationToken ct);
}
