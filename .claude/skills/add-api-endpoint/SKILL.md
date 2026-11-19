---
name: add-api-endpoint
description: Add a new billing API endpoint end to end (domain rule, use case, minimal API route, tests) following this repo's Clean Architecture. Use when asked to add or change an HTTP endpoint or a new invoice operation.
---

# Add an API endpoint

Follow these steps in order. Do not skip the tests.

1. **Clarify the contract first.** Write down: route + verb, request body, success response,
   and every failure (with its error code and HTTP status). If anything is ambiguous, ask.

2. **Domain rule (if the operation changes state).**
   - Add a method to the aggregate in `src/Billing.Domain/Invoices/Invoice.cs` returning `Result`.
   - Add any new error to `InvoiceErrors.cs` with a stable `invoice.<snake_case>` code.
   - Write tests first in `tests/Billing.Domain.Tests` covering the happy path and every rule.

3. **Use case.** Add a method to `InvoiceService`. State changes must go through the private
   `MutateAsync` helper (load -> apply -> save on success). No business rules here.
   Test in `tests/Billing.Application.Tests` using the hand-rolled fake repository,
   including "failed operation is not saved".

4. **HTTP adapter.** Add the route in `src/Billing.Api/InvoiceEndpoints.cs`.
   - Validate malformed input -> `Results.ValidationProblem` (400).
   - Map the use-case result through `ToHttp` (do not hand-roll status codes).
   - Add a test in `tests/Billing.Api.Tests` for success and for each status code.

5. **Frontend contract.** If the DTO changed, update `web/src/features/invoices/types.ts`.

6. **Verify.** Run `dotnet build` and `dotnet test`; if `web/` changed, run lint, typecheck and tests.
   Report the actual results. Update the `PLAN.md` checkbox.

## Checklist before you finish

- [ ] No business logic in endpoints or services
- [ ] Money uses `Money`, time uses `TimeProvider`
- [ ] Every new error code has a test asserting it
- [ ] Payments or auth touched? Flag it for human review in your summary
