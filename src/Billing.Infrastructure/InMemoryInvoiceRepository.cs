using System.Runtime.CompilerServices;
using Billing.Application;
using Billing.Domain.Invoices;

namespace Billing.Infrastructure;

/// <summary>
/// Placeholder adapter so the template runs with no database. It behaves like one: it stores
/// immutable snapshots, hands out independent copies, and rejects stale writes using a version
/// number (optimistic concurrency, the same pattern as an EF Core rowversion).
/// PLAN.md tracks replacing it with EF Core + SQL Server.
/// </summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, (InvoiceSnapshot Snapshot, int Version)> _store = [];

    // Remembers which version each handed-out instance was loaded at, without changing the domain model.
    private readonly ConditionalWeakTable<Invoice, StrongBox<int>> _loadedVersions = [];

    public Task<Invoice?> GetAsync(Guid id, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.TryGetValue(id, out var entry) ? Load(entry) : null);
        }
    }

    public Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Invoice>>([.. _store.Values.Select(Load)]);
        }
    }

    public Task<bool> SaveAsync(Invoice invoice, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        lock (_gate)
        {
            var expectedVersion = _loadedVersions.TryGetValue(invoice, out var loaded) ? loaded.Value : 0;
            var currentVersion = _store.TryGetValue(invoice.Id, out var entry) ? entry.Version : 0;
            if (currentVersion != expectedVersion)
            {
                return Task.FromResult(false);
            }

            var newVersion = currentVersion + 1;
            _store[invoice.Id] = (invoice.ToSnapshot(), newVersion);
            _loadedVersions.AddOrUpdate(invoice, new StrongBox<int>(newVersion));
            return Task.FromResult(true);
        }
    }

    private Invoice Load((InvoiceSnapshot Snapshot, int Version) entry)
    {
        var invoice = Invoice.FromSnapshot(entry.Snapshot);
        _loadedVersions.AddOrUpdate(invoice, new StrongBox<int>(entry.Version));
        return invoice;
    }
}
