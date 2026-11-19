using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>
/// Persistence port for invoices. The application layer depends on this interface; Billing.Infrastructure
/// provides the implementation (Dependency Inversion), so storage can change without touching business code.
/// </summary>
/// <remarks>
/// Contract every implementation must honour (verified by Billing.Infrastructure.Tests):
/// reads return independent copies, and saves use optimistic concurrency.
/// </remarks>
public interface IInvoiceRepository
{
    /// <summary>Loads one invoice as a copy the caller owns; changes are invisible to others until saved.</summary>
    /// <param name="id">The id of the invoice to load.</param>
    /// <param name="ct">Cancellation token for the storage call.</param>
    /// <returns>The invoice, or <c>null</c> if no invoice has that id.</returns>
    Task<Invoice?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Loads all invoices, each as an independent copy.</summary>
    /// <param name="ct">Cancellation token for the storage call.</param>
    /// <returns>All invoices; empty if there are none.</returns>
    Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct);

    /// <summary>Saves a new or changed invoice with optimistic concurrency.</summary>
    /// <param name="invoice">A newly created invoice, or one loaded from this repository and then changed.</param>
    /// <param name="ct">Cancellation token for the storage call.</param>
    /// <returns>
    /// <c>true</c> if saved; <c>false</c> (and nothing saved) if someone else changed the invoice after this
    /// instance was loaded. The caller should report a conflict rather than retry blindly.
    /// </returns>
    Task<bool> SaveAsync(Invoice invoice, CancellationToken ct);
}
