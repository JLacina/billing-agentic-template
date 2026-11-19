using Billing.Application;
using Billing.Domain.Common;
using Billing.Domain.Invoices;
using FluentAssertions;

namespace Billing.Application.Tests;

public class InvoiceServiceTests
{
    private readonly FakeInvoiceRepository _repository = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
    private readonly InvoiceService _sut;

    public InvoiceServiceTests() => _sut = new InvoiceService(_repository, _clock);

    private async Task<Guid> CreateDraftAsync()
    {
        var created = await _sut.CreateAsync(new CreateInvoiceRequest("cust-1", "GBP"), CancellationToken.None);
        return created.Value.Id;
    }

    [Fact]
    public async Task CreateAsync_PersistsDraft()
    {
        var id = await CreateDraftAsync();

        _repository.Saved.Should().ContainKey(id);
        _repository.Saved[id].Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task CreateAsync_InvalidCurrency_ReturnsValidationErrorAndSavesNothing()
    {
        var result = await _sut.CreateAsync(new CreateInvoiceRequest("cust-1", "123"), CancellationToken.None);

        result.Error.Should().Be(InvoiceErrors.UnsupportedCurrency);
        _repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Error.Should().Be(InvoiceErrors.NotFound);
    }

    [Fact]
    public async Task IssueAsync_UsesInjectedClockForIssueDate()
    {
        var id = await CreateDraftAsync();
        await _sut.AddLineAsync(id, new AddLineRequest("Box seat", 1, 50m, 0.2m), CancellationToken.None);

        var result = await _sut.IssueAsync(id, CancellationToken.None);

        result.Value.IssuedOn.Should().Be(new DateOnly(2026, 3, 1));
        result.Value.Total.Should().Be(60m);
    }

    [Fact]
    public async Task FailedDomainOperation_IsNotSaved()
    {
        var id = await CreateDraftAsync();
        var savesBefore = _repository.SaveCount;

        var result = await _sut.IssueAsync(id, CancellationToken.None); // no lines -> fails

        result.Error.Should().Be(InvoiceErrors.NoLines);
        _repository.SaveCount.Should().Be(savesBefore);
    }

    [Fact]
    public async Task AddLineAsync_OverflowingPrice_IsRejectedAndNotSaved()
    {
        var id = await CreateDraftAsync();
        var savesBefore = _repository.SaveCount;

        var result = await _sut.AddLineAsync(id, new AddLineRequest("huge", 2, decimal.MaxValue, 0m), CancellationToken.None);

        result.Error.Should().Be(InvoiceErrors.AmountTooLarge);
        _repository.SaveCount.Should().Be(savesBefore);
    }

    [Fact]
    public async Task RecordPaymentAsync_UpdatesOutstanding()
    {
        var id = await CreateDraftAsync();
        await _sut.AddLineAsync(id, new AddLineRequest("Hot dog", 4, 5m, 0m), CancellationToken.None);
        await _sut.IssueAsync(id, CancellationToken.None);

        var result = await _sut.RecordPaymentAsync(id, new RecordPaymentRequest(15m), CancellationToken.None);

        result.Value.Status.Should().Be(nameof(InvoiceStatus.PartiallyPaid));
        result.Value.Outstanding.Should().Be(5m);
    }

    [Fact]
    public async Task Mutation_WhenRepositoryReportsConflict_ReturnsConflictError()
    {
        var id = await CreateDraftAsync();
        _repository.RejectNextSave = true;

        var result = await _sut.AddLineAsync(id, new AddLineRequest("Pie", 1, 4m, 0m), CancellationToken.None);

        result.Error.Should().Be(InvoiceErrors.ConcurrencyConflict);
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    // Hand-rolled fakes: simpler and more readable than a mocking library for a small port.
    private sealed class FakeInvoiceRepository : IInvoiceRepository
    {
        public Dictionary<Guid, Invoice> Saved { get; } = [];
        public int SaveCount { get; private set; }
        public bool RejectNextSave { get; set; }

        public Task<Invoice?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Saved.GetValueOrDefault(id));

        public Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Invoice>>([.. Saved.Values]);

        public Task<bool> SaveAsync(Invoice invoice, CancellationToken ct)
        {
            if (RejectNextSave)
            {
                RejectNextSave = false;
                return Task.FromResult(false);
            }

            Saved[invoice.Id] = invoice;
            SaveCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
