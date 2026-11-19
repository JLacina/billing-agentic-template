using System.Net;
using System.Net.Http.Json;
using Billing.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Billing.Api.Tests;

/// <summary>Integration tests: real HTTP pipeline, routing, serialization and DI, in memory.</summary>
public class InvoiceApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<InvoiceDto> CreateInvoiceAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest("cust-1", "GBP"));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<InvoiceDto>())!;
    }

    [Fact]
    public async Task Health_ReturnsOk() =>
        (await _client.GetAsync(new Uri("/health", UriKind.Relative))).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task FullInvoiceLifecycle_Works()
    {
        var invoice = await CreateInvoiceAsync();

        await _client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/lines", new AddLineRequest("Corporate box", 1, 1000m, 0.2m));
        await _client.PostAsync(new Uri($"/api/invoices/{invoice.Id}/issue", UriKind.Relative), null);
        var paid = await _client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/payments", new RecordPaymentRequest(1200m));

        paid.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await paid.Content.ReadFromJsonAsync<InvoiceDto>();
        body!.Status.Should().Be("Paid");
        body.Outstanding.Should().Be(0m);
    }

    [Fact]
    public async Task CreateInvoice_InvalidCurrency_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/invoices", new CreateInvoiceRequest("cust-1", "POUNDS"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetInvoice_UnknownId_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync(new Uri($"/api/invoices/{Guid.NewGuid()}", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task BusinessRuleViolation_Returns422WithErrorCode()
    {
        var invoice = await CreateInvoiceAsync();

        var response = await _client.PostAsync(new Uri($"/api/invoices/{invoice.Id}/issue", UriKind.Relative), null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().Should().Be("invoice.no_lines");
    }
}
