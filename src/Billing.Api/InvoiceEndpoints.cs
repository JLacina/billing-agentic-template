using Billing.Application;
using Billing.Domain.Common;
using Billing.Domain.Invoices;

namespace Billing.Api;

/// <summary>HTTP adapter only: map requests to use cases and results to status codes. No business logic.</summary>
public static class InvoiceEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapGet("/", async (InvoiceService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct).ConfigureAwait(false)));

        group.MapGet("/{id:guid}", async (Guid id, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.GetAsync(id, ct).ConfigureAwait(false)));

        // Validation lives in the domain; the endpoint only translates the result.
        group.MapPost("/", async (CreateInvoiceRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.CreateAsync(req, ct).ConfigureAwait(false),
                dto => Results.Created($"/api/invoices/{dto.Id}", dto)));

        group.MapPost("/{id:guid}/lines", async (Guid id, AddLineRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.AddLineAsync(id, req, ct).ConfigureAwait(false)));

        group.MapPost("/{id:guid}/issue", async (Guid id, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.IssueAsync(id, ct).ConfigureAwait(false)));

        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.RecordPaymentAsync(id, req, ct).ConfigureAwait(false)));

        return app;
    }

    // DRY: one place decides how business errors become HTTP responses (RFC 7807 ProblemDetails).
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
