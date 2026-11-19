using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>Persistence port. Implemented in Billing.Infrastructure (Dependency Inversion).</summary>
public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct);
    Task SaveAsync(Invoice invoice, CancellationToken ct);
}
