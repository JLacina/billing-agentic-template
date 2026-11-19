using System.Runtime.CompilerServices;
using Billing.Application;
using Billing.Domain.Invoices;

namespace Billing.Infrastructure;

/// <summary>
/// Placeholder adapter so the template runs with no database. It behaves like one: it stores
/// detached snapshots, returns independent invoice instances, and rejects stale writes using a version
/// number (optimistic concurrency, the same pattern as an EF Core rowversion).
/// PLAN.md tracks replacing it with EF Core + SQL Server.
/// </summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, (InvoiceSnapshot Snapshot, int Version)> _store = [];

    // Remembers which version each handed-out instance was loaded at, without changing the domain model.
    private readonly ConditionalWeakTable<Invoice, StrongBox<int>> _loadedVersions = [];

    /// <summary>
    /// Loads one invoice as a new, independent instance built from the stored snapshot.
    /// Changes made to it are invisible to other callers until <see cref="SaveAsync"/> succeeds.
    /// </summary>
    /// <param name="id">The id of the invoice to load.</param>
    /// <param name="ct">Cancellation token (not used by the in-memory store; kept for the interface contract).</param>
    /// <returns>A copy of the invoice, or <c>null</c> if no invoice has that id.</returns>
    public Task<Invoice?> GetAsync(Guid id, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult(_store.TryGetValue(id, out var entry) ? Load(entry) : null);
        }
    }

    /// <summary>Loads every stored invoice, each as a new, independent instance.</summary>
    /// <param name="ct">Cancellation token (not used by the in-memory store; kept for the interface contract).</param>
    /// <returns>All invoices, in no particular order. Empty if none exist.</returns>
    public Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Invoice>>([.. _store.Values.Select(Load)]);
        }
    }

    /// <summary>
    /// Saves a new or changed invoice, refusing the write if someone else saved the same invoice
    /// after this instance was loaded (optimistic concurrency).
    /// </summary>
    /// <param name="invoice">
    /// The invoice to save: either one created by <see cref="Invoice.CreateDraft"/>, or one previously
    /// returned by <see cref="GetAsync"/> / <see cref="ListAsync"/> and then changed.
    /// </param>
    /// <param name="ct">Cancellation token (not used by the in-memory store; kept for the interface contract).</param>
    /// <returns>
    /// <c>true</c> if the invoice was saved; <c>false</c> if the stored invoice changed since this instance
    /// was loaded. Nothing is saved in that case, and the caller should report a conflict.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="invoice"/> is <c>null</c>.</exception>
    /// <remarks>
    /// How the stale-write check works: each instance handed out remembers the version it was loaded at.
    /// On save, that version must still equal the stored version; if another request saved in between,
    /// the versions differ and the save is refused. A brand-new invoice has expected and stored version 0.
    /// The lock makes the check and the write one atomic step.
    /// </remarks>
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

    /// <summary>
    /// Rebuilds a fresh <see cref="Invoice"/> from a stored snapshot and records which version it came from,
    /// so <see cref="SaveAsync"/> can later detect whether someone else changed it in the meantime.
    /// </summary>
    /// <param name="entry">The stored snapshot and the version number it was saved with.</param>
    /// <returns>A new invoice instance that the caller owns.</returns>
    private Invoice Load((InvoiceSnapshot Snapshot, int Version) entry)
    {
        var invoice = Invoice.FromSnapshot(entry.Snapshot);
        _loadedVersions.AddOrUpdate(invoice, new StrongBox<int>(entry.Version));
        return invoice;
    }
}
