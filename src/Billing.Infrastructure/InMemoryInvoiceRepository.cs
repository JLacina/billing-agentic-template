using System.Collections.Concurrent;
using Billing.Application;
using Billing.Domain.Invoices;

namespace Billing.Infrastructure;

/// <summary>
/// Placeholder adapter so the template runs with no database.
/// PLAN.md tracks replacing it with an EF Core + SQL Server implementation.
/// </summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<Guid, Invoice> _store = new();

    public Task<Invoice?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Invoice>>([.. _store.Values]);

    public Task SaveAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        _store[invoice.Id] = invoice;
        return Task.CompletedTask;
    }
}
