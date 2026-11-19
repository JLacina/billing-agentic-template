# Code walkthrough

What each part of the code is meant to do, and why it is built that way. Read this before changing
the code or using the repository as a template. Detailed contracts are in the XML comments on each
class and method (the build fails if a public member in `src/` is undocumented).

## How a request flows

```
HTTP request
   │
   ▼
Billing.Api            InvoiceEndpoints      maps routes, calls a use case, turns the Result into HTTP
   │
   ▼
Billing.Application    InvoiceService        orchestrates: load → call the domain → save
   │                   IInvoiceRepository    persistence port (interface)
   ▼
Billing.Domain         Invoice, Money, ...   all business rules; no framework dependencies
   ▲
   │ implements IInvoiceRepository
Billing.Infrastructure InMemoryInvoiceRepository   storage adapter (snapshots + optimistic concurrency)
```

Dependencies point inward: `Domain` references nothing, `Application` references only `Domain`,
`Api` and `Infrastructure` sit on the outside. See ADR 0001.

**Example: `POST /api/invoices/{id}/payments` with `{ "amount": 500 }`**

1. `InvoiceEndpoints` receives the request and calls `InvoiceService.RecordPaymentAsync`.
2. `InvoiceService.MutateAsync` loads the invoice from `IInvoiceRepository` (a private copy).
3. `Invoice.RecordPayment` checks status, currency, amount > 0 and amount ≤ outstanding, then updates
   `AmountPaid` and `Status`. On any failure it returns an `Error` and changes nothing.
4. If the domain succeeded, `MutateAsync` saves. If someone else saved the invoice in the meantime,
   the save is refused and the result is `invoice.concurrency_conflict`.
5. `InvoiceEndpoints.ToHttp` returns 200 with the invoice, or ProblemDetails with the status picked
   from the error type (400 / 404 / 409 / 422) and the stable error `code`.

## Billing.Domain: business rules

| Type | Meant to do | Key members |
|---|---|---|
| `Invoice` | The aggregate root. Owns every invoice rule so nothing outside can put an invoice in an invalid state. | `CreateDraft` (validates customer and currency), `AddLine` (validates the line and the `MaxAmount` limits **before** changing anything), `Issue` (draft with at least one line), `RecordPayment` (no overpayment; moves to PartiallyPaid or Paid), `Total`, `Outstanding`, `ToSnapshot` / `FromSnapshot` (persistence only) |
| `InvoiceLine` | One immutable line. | `Net` = quantity × price, `Tax` = net × rate (rounded per line), `Gross` = net + tax |
| `InvoiceStatus` | The lifecycle: Draft → Issued → PartiallyPaid → Paid. | |
| `InvoiceErrors` | Every expected failure, each with a stable code (part of the API contract) and an `ErrorType`. | e.g. `AmountTooLarge`, `Overpayment`, `ConcurrencyConflict` |
| `Money` | Amount + currency as one value. Never `double`. Always 2 decimal places, banker's rounding. | `Of` (throws for an unsupported currency), `Add` / `Subtract` (throw on currency mismatch), `Multiply` |
| `Currencies` | The explicit list of currencies that can be invoiced (GBP, EUR, USD). | `TryNormalize` trims and upper-cases, and fails for anything not supported |
| `Result`, `Result<T>`, `Error`, `ErrorType` | Expected failures as values, not exceptions. | `IsSuccess`, `Error`, `Value` (throws on failure) |

**Why validate before mutating?** A review found that adding an oversized line *first* and checking
later left a corrupted invoice whose total overflowed on every read, breaking the list endpoint for
everyone. Now every check runs first, and a rejected call leaves the invoice exactly as it was (ADR 0002).

**Why `Result` instead of exceptions?** Business failures (overpayment, wrong state) are normal,
expected outcomes. Returning them as values makes them visible in method signatures and easy to map
to HTTP in one place. Exceptions are kept for programming errors, such as adding GBP to EUR in code.

## Billing.Application: use cases

| Type | Meant to do |
|---|---|
| `InvoiceService` | One method per use case: `CreateAsync`, `GetAsync`, `ListAsync`, `AddLineAsync`, `IssueAsync`, `RecordPaymentAsync`. It orchestrates only and contains no business rules. |
| `InvoiceService.MutateAsync` (private) | The shared flow for every change: load, apply one domain operation, save only if it succeeded, and report a conflict if the save is refused. Keeps every use case a one-liner (DRY). |
| `IInvoiceRepository` | The persistence port. Contract: reads return independent copies; `SaveAsync` returns `false` on a stale write. |
| `InvoiceDto`, `*Request` records | The API contract. `InvoiceDto` is mirrored in `web/src/features/invoices/types.ts`. |

Time comes from an injected `TimeProvider`, never `DateTime.Now`, so tests can fix the date.

## Billing.Infrastructure: storage

| Type | Meant to do |
|---|---|
| `InMemoryInvoiceRepository` | A stand-in for a database that behaves like one. Stores detached snapshots with a version number, returns independent invoice instances, and refuses a save if the stored version changed since the copy was loaded. |

**Why not just store the objects?** Sharing one mutable `Invoice` between requests meant two
concurrent payments could both pass the balance check and overpay. Copies plus a version check
(optimistic concurrency) make that impossible; the slower request gets a 409. EF Core will use the
same contract with a rowversion column.

## Billing.Api: HTTP

| Member | Meant to do |
|---|---|
| `InvoiceEndpoints.MapInvoiceEndpoints` | Registers the six `/api/invoices` routes. Each route only calls a use case. No validation or business logic. |
| `InvoiceEndpoints.ToHttp` (private) | The single place that converts a `Result` into HTTP: 200/201 on success, or ProblemDetails with Validation 400, NotFound 404, Conflict 409, Rule 422 and the error `code`. |
| `Program.cs` | Composition root: registers services (`TimeProvider`, repository, `InvoiceService`), ProblemDetails, CORS, HTTPS, health check. |

## web: React frontend

| File | Meant to do |
|---|---|
| `features/invoices/InvoiceList.tsx` | Shows invoices with explicit loading, empty, error and loaded states. Receives `loadInvoices` as a prop so tests don't need a network. |
| `features/invoices/invoicesApi.ts` | Thin `fetch` wrapper; throws with the HTTP status on failure. |
| `features/invoices/types.ts` | TypeScript mirror of `InvoiceDto`. |
| `shared/formatMoney.ts` | The one place money is formatted (`Intl.NumberFormat`). Never throws: an unknown currency falls back to plain text so one bad record can't break the page. |

## Tests: where each kind of check lives

| Project | Checks | Style |
|---|---|---|
| `Billing.Domain.Tests` | Every business rule, limit and calculation | Plain xUnit, no mocks |
| `Billing.Application.Tests` | Use-case orchestration: failures not saved, conflicts reported, clock used | Hand-rolled fakes |
| `Billing.Api.Tests` | HTTP contract: routes, status codes, ProblemDetails codes, overflow regression | `WebApplicationFactory` |
| `Billing.Infrastructure.Tests` | Repository contract: copies, stale writes, 50 concurrent payments | Real repository |
| `web/src/**/*.test.ts(x)` | UI states and helpers | Vitest + Testing Library |

## Adding a feature (the short version)

1. Write the acceptance criteria in `PLAN.md`.
2. Domain first: add a method on the aggregate that returns `Result`, plus any new `InvoiceErrors`
   entry with the right `ErrorType`. Test it.
3. Add the use case to `InvoiceService` through `MutateAsync`. Test it with the fakes.
4. Add the route in `InvoiceEndpoints` using `ToHttp`. Test the status codes.
5. Document every new public member (the build enforces it) and run `/pre-pr-check`.

The `/add-api-endpoint` skill walks an agent through exactly these steps.
