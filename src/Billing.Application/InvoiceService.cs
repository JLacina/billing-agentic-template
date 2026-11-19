using Billing.Domain.Common;
using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>
/// Use cases for invoices. Orchestrates only: load, call the domain, save.
/// Business rules live in <see cref="Invoice"/>, not here.
/// </summary>
/// <param name="repository">Where invoices are loaded from and saved to.</param>
/// <param name="clock">Source of the current time, injected so tests can fix the date.</param>
public sealed class InvoiceService(IInvoiceRepository repository, TimeProvider clock)
{
    /// <summary>Creates and saves a new, empty draft invoice.</summary>
    /// <param name="request">Customer id and currency for the new invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The created invoice; or <see cref="InvoiceErrors.InvalidCustomer"/> / <see cref="InvoiceErrors.UnsupportedCurrency"/>
    /// if the request is invalid (nothing is saved).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    public async Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = Invoice.CreateDraft(request.CustomerId, request.Currency);
        if (!created.IsSuccess)
        {
            return Result<InvoiceDto>.Failure(created.Error!);
        }

        return await repository.SaveAsync(created.Value, ct).ConfigureAwait(false)
            ? Result<InvoiceDto>.Success(InvoiceDto.From(created.Value))
            : Result<InvoiceDto>.Failure(InvoiceErrors.ConcurrencyConflict);
    }

    /// <summary>Gets one invoice by id.</summary>
    /// <param name="id">The invoice id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice, or <see cref="InvoiceErrors.NotFound"/>.</returns>
    public async Task<Result<InvoiceDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var invoice = await repository.GetAsync(id, ct).ConfigureAwait(false);
        return invoice is null
            ? Result<InvoiceDto>.Failure(InvoiceErrors.NotFound)
            : Result<InvoiceDto>.Success(InvoiceDto.From(invoice));
    }

    /// <summary>Lists all invoices. Fine for the in-memory example; a real store would need paging and filters.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All invoices; empty if there are none.</returns>
    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct)
    {
        var invoices = await repository.ListAsync(ct).ConfigureAwait(false);
        return [.. invoices.Select(InvoiceDto.From)];
    }

    /// <summary>Adds a line to a draft invoice.</summary>
    /// <param name="id">The invoice to add the line to.</param>
    /// <param name="request">Description, quantity, unit price (in the invoice's currency) and tax rate (a fraction, e.g. 0.20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updated invoice; a validation or rule error from <see cref="Invoice.AddLine"/> (e.g.
    /// <see cref="InvoiceErrors.AmountTooLarge"/>, <see cref="InvoiceErrors.NotDraft"/>); <see cref="InvoiceErrors.NotFound"/>;
    /// or <see cref="InvoiceErrors.ConcurrencyConflict"/> if the invoice changed meanwhile.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    public Task<Result<InvoiceDto>> AddLineAsync(Guid id, AddLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateAsync(id, invoice => invoice.AddLine(
            request.Description,
            request.Quantity,
            Money.Of(request.UnitPrice, invoice.Currency),
            request.TaxRate), ct);
    }

    /// <summary>Issues a draft invoice, dated today (UTC) according to the injected <see cref="TimeProvider"/>.</summary>
    /// <param name="id">The invoice to issue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The issued invoice; <see cref="InvoiceErrors.NotDraft"/> or <see cref="InvoiceErrors.NoLines"/>;
    /// <see cref="InvoiceErrors.NotFound"/>; or <see cref="InvoiceErrors.ConcurrencyConflict"/>.
    /// </returns>
    public Task<Result<InvoiceDto>> IssueAsync(Guid id, CancellationToken ct) =>
        MutateAsync(id, invoice => invoice.Issue(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), ct);

    /// <summary>Records money already received against an issued invoice.</summary>
    /// <param name="id">The invoice being paid.</param>
    /// <param name="request">The amount received, in the invoice's currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updated invoice (PartiallyPaid or Paid); <see cref="InvoiceErrors.InvalidPayment"/>,
    /// <see cref="InvoiceErrors.Overpayment"/> or <see cref="InvoiceErrors.NotPayable"/>; <see cref="InvoiceErrors.NotFound"/>;
    /// or <see cref="InvoiceErrors.ConcurrencyConflict"/> if another payment was saved first.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <c>null</c>.</exception>
    public Task<Result<InvoiceDto>> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateAsync(id, invoice => invoice.RecordPayment(Money.Of(request.Amount, invoice.Currency)), ct);
    }

    /// <summary>
    /// Shared flow for every state-changing use case: load the invoice, apply one domain operation,
    /// and save only if the operation succeeded.
    /// </summary>
    /// <param name="id">Invoice to change.</param>
    /// <param name="operation">The domain method to run, e.g. <c>invoice => invoice.Issue(today)</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updated invoice; <see cref="InvoiceErrors.NotFound"/>; the domain's own error (nothing is saved);
    /// or <see cref="InvoiceErrors.ConcurrencyConflict"/> if someone else saved the invoice after it was loaded,
    /// in which case the caller gets a 409 and should retry with fresh data.
    /// </returns>
    /// <remarks>DRY: keeps the use cases one-liners and guarantees a failed operation is never persisted.</remarks>
    private async Task<Result<InvoiceDto>> MutateAsync(Guid id, Func<Invoice, Result> operation, CancellationToken ct)
    {
        var invoice = await repository.GetAsync(id, ct).ConfigureAwait(false);
        if (invoice is null)
        {
            return Result<InvoiceDto>.Failure(InvoiceErrors.NotFound);
        }

        var result = operation(invoice);
        if (!result.IsSuccess)
        {
            return Result<InvoiceDto>.Failure(result.Error!);
        }

        return await repository.SaveAsync(invoice, ct).ConfigureAwait(false)
            ? Result<InvoiceDto>.Success(InvoiceDto.From(invoice))
            : Result<InvoiceDto>.Failure(InvoiceErrors.ConcurrencyConflict);
    }
}
