using Billing.Application;
using Billing.Domain.Common;

namespace Billing.Api;

/// <summary>HTTP adapter only: map requests to use cases and results to status codes. No business logic.</summary>
public static class InvoiceEndpoints
{
    /// <summary>
    /// Maps the <c>/api/invoices</c> routes: list, get, create, add line, issue and record payment.
    /// Errors are returned as RFC 7807 ProblemDetails with a stable <c>code</c>.
    /// </summary>
    /// <param name="app">The application's route builder (called from <c>Program.cs</c>).</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Invoices");

        // GET /api/invoices -> 200 with all invoices.
        group.MapGet("/", async (InvoiceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct).ConfigureAwait(false)));

        // GET /api/invoices/{id} -> 200 with the invoice, or 404 invoice.not_found.
        group.MapGet("/{id:guid}", async (Guid id, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.GetAsync(id, ct).ConfigureAwait(false)));

        // POST /api/invoices -> 201 Created with a Location header, or 400 for an invalid customer/currency.
        // Validation lives in the domain; the endpoint only translates the result.
        group.MapPost("/", async (CreateInvoiceRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.CreateAsync(req, ct).ConfigureAwait(false),
                dto => Results.Created($"/api/invoices/{dto.Id}", dto)));

        // POST /api/invoices/{id}/lines -> 200, 400 (invalid line / limits), 404, 409, or 422 (not a draft).
        group.MapPost("/{id:guid}/lines", async (Guid id, AddLineRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.AddLineAsync(id, req, ct).ConfigureAwait(false)));

        // POST /api/invoices/{id}/issue -> 200, 404, 409, or 422 (not a draft / no lines).
        group.MapPost("/{id:guid}/issue", async (Guid id, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.IssueAsync(id, ct).ConfigureAwait(false)));

        // POST /api/invoices/{id}/payments -> 200, 400 (invalid amount), 404, 409, or 422 (not payable / overpayment).
        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.RecordPaymentAsync(id, req, ct).ConfigureAwait(false)));

        return app;
    }

    /// <summary>
    /// Turns a use-case result into an HTTP response. Success returns 200 (or the custom
    /// <paramref name="onSuccess"/> response, e.g. 201 Created). Failure returns RFC 7807 ProblemDetails
    /// whose status comes from the error's <see cref="ErrorType"/>: Validation 400, NotFound 404,
    /// Conflict 409, Rule 422. The stable error code goes in the <c>code</c> extension for clients.
    /// </summary>
    /// <param name="result">The use-case outcome to translate.</param>
    /// <param name="onSuccess">Optional custom success response; defaults to 200 OK with the invoice.</param>
    /// <returns>The HTTP response to send.</returns>
    /// <remarks>DRY: the only place that decides status codes, so every endpoint behaves the same way.</remarks>
    private static IResult ToHttp(Result<InvoiceDto> result, Func<InvoiceDto, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value);
        }

        var error = result.Error!;
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        return Results.Problem(title: error.Message, statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["code"] = error.Code,
        });
    }
}
