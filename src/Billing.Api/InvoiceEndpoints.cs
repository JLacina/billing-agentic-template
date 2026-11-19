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

        group.MapPost("/", async (CreateInvoiceRequest req, InvoiceService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.CustomerId) || req.Currency?.Length != 3)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["request"] = ["CustomerId is required and Currency must be a 3-letter ISO code."],
                });
            }

            var result = await svc.CreateAsync(req, ct).ConfigureAwait(false);
            return Results.Created($"/api/invoices/{result.Value.Id}", result.Value);
        });

        group.MapPost("/{id:guid}/lines", async (Guid id, AddLineRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.AddLineAsync(id, req, ct).ConfigureAwait(false)));

        group.MapPost("/{id:guid}/issue", async (Guid id, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.IssueAsync(id, ct).ConfigureAwait(false)));

        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest req, InvoiceService svc, CancellationToken ct) =>
            ToHttp(await svc.RecordPaymentAsync(id, req, ct).ConfigureAwait(false)));

        return app;
    }

    // DRY: one place decides how business errors become HTTP responses (RFC 7807 ProblemDetails).
    private static IResult ToHttp(Result<InvoiceDto> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        var error = result.Error!;
        var status = error == InvoiceErrors.NotFound
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;

        return Results.Problem(title: error.Message, statusCode: status, extensions: new Dictionary<string, object?>
        {
            ["code"] = error.Code,
        });
    }
}
