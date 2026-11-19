using Billing.Domain.Common;
using Billing.Domain.Invoices;

namespace Billing.Application;

/// <summary>
/// Use cases for invoices. Orchestrates only: load, call the domain, save.
/// Business rules live in <see cref="Invoice"/>, not here.
/// </summary>
public sealed class InvoiceService(IInvoiceRepository repository, TimeProvider clock)
{
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

    public async Task<Result<InvoiceDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var invoice = await repository.GetAsync(id, ct).ConfigureAwait(false);
        return invoice is null
            ? Result<InvoiceDto>.Failure(InvoiceErrors.NotFound)
            : Result<InvoiceDto>.Success(InvoiceDto.From(invoice));
    }

    public async Task<IReadOnlyList<InvoiceDto>> ListAsync(CancellationToken ct)
    {
        var invoices = await repository.ListAsync(ct).ConfigureAwait(false);
        return [.. invoices.Select(InvoiceDto.From)];
    }

    public Task<Result<InvoiceDto>> AddLineAsync(Guid id, AddLineRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateAsync(id, invoice => invoice.AddLine(
            request.Description,
            request.Quantity,
            Money.Of(request.UnitPrice, invoice.Currency),
            request.TaxRate), ct);
    }

    public Task<Result<InvoiceDto>> IssueAsync(Guid id, CancellationToken ct) =>
        MutateAsync(id, invoice => invoice.Issue(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), ct);

    public Task<Result<InvoiceDto>> RecordPaymentAsync(Guid id, RecordPaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MutateAsync(id, invoice => invoice.RecordPayment(Money.Of(request.Amount, invoice.Currency)), ct);
    }

    // DRY: every state-changing use case is "load -> apply domain operation -> save if it succeeded".
    // A concurrent change makes the save fail; the caller gets a 409 and retries with fresh data.
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
