using Billing.Application;
using Billing.Domain.Common;
using Billing.Domain.Invoices;
using Billing.Infrastructure;
using FluentAssertions;

namespace Billing.Infrastructure.Tests;

/// <summary>
/// Adapter tests: the in-memory repository must behave like a real database with optimistic
/// concurrency, so the application layer can't accidentally rely on shared mutable objects.
/// </summary>
public class InMemoryInvoiceRepositoryTests
{
    private readonly InMemoryInvoiceRepository _repository = new();
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<Guid> SaveIssuedInvoiceAsync(decimal amount)
    {
        var invoice = Invoice.CreateDraft("cust-1", "GBP").Value;
        invoice.AddLine("Ticket", 1, Money.Of(amount, "GBP"), 0m);
        invoice.Issue(new DateOnly(2026, 1, 1));
        (await _repository.SaveAsync(invoice, Ct)).Should().BeTrue();
        return invoice.Id;
    }

    [Fact]
    public async Task GetAsync_ReturnsIndependentCopies()
    {
        var id = await SaveIssuedInvoiceAsync(100m);

        var first = await _repository.GetAsync(id, Ct);
        var second = await _repository.GetAsync(id, Ct);

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public async Task UnsavedChanges_AreNotVisibleToOtherReaders()
    {
        var id = await SaveIssuedInvoiceAsync(100m);
        var loaded = (await _repository.GetAsync(id, Ct))!;

        loaded.RecordPayment(Money.Of(40m, "GBP"));

        (await _repository.GetAsync(id, Ct))!.AmountPaid.Should().Be(Money.Zero("GBP"));
    }

    [Fact]
    public async Task StaleWrite_IsRejected()
    {
        var id = await SaveIssuedInvoiceAsync(100m);
        var a = (await _repository.GetAsync(id, Ct))!;
        var b = (await _repository.GetAsync(id, Ct))!;

        a.RecordPayment(Money.Of(100m, "GBP"));
        b.RecordPayment(Money.Of(100m, "GBP"));

        (await _repository.SaveAsync(a, Ct)).Should().BeTrue();
        (await _repository.SaveAsync(b, Ct)).Should().BeFalse("b was loaded before a's payment was saved");
        (await _repository.GetAsync(id, Ct))!.AmountPaid.Should().Be(Money.Of(100m, "GBP"));
    }

    [Fact]
    public async Task SameInstance_CanBeSavedRepeatedly()
    {
        var id = await SaveIssuedInvoiceAsync(100m);
        var invoice = (await _repository.GetAsync(id, Ct))!;

        invoice.RecordPayment(Money.Of(10m, "GBP"));
        (await _repository.SaveAsync(invoice, Ct)).Should().BeTrue();
        invoice.RecordPayment(Money.Of(10m, "GBP"));
        (await _repository.SaveAsync(invoice, Ct)).Should().BeTrue();

        (await _repository.GetAsync(id, Ct))!.AmountPaid.Should().Be(Money.Of(20m, "GBP"));
    }

    // Reviewer finding: two concurrent payments could both pass the balance check and overpay.
    [Fact]
    public async Task ConcurrentPayments_NeverOverpayOrLoseUpdates()
    {
        var id = await SaveIssuedInvoiceAsync(100m);
        var service = new InvoiceService(_repository, TimeProvider.System);

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
            service.RecordPaymentAsync(id, new RecordPaymentRequest(10m), Ct))));

        var succeeded = results.Count(r => r.IsSuccess);
        var stored = (await _repository.GetAsync(id, Ct))!;

        stored.AmountPaid.Amount.Should().Be(succeeded * 10m, "every successful payment must be persisted");
        stored.AmountPaid.Amount.Should().BeLessThanOrEqualTo(100m, "the balance can never be overpaid");
        results.Where(r => !r.IsSuccess).Select(r => r.Error)
            .Should().OnlyContain(e => e == InvoiceErrors.ConcurrencyConflict || e == InvoiceErrors.Overpayment || e == InvoiceErrors.NotPayable);
    }
}
