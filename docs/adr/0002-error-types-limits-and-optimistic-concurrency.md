# ADR 0002: Error types, input limits and optimistic concurrency

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

A code review of the template found three defects:

1. A line with a huge unit price was added to the invoice before the total was calculated. The total
   then overflowed `decimal`, the request returned 500, and the corrupted invoice broke `GET` and
   list requests for everyone.
2. The in-memory repository handed out shared mutable `Invoice` objects. Two concurrent payments
   could both pass the outstanding-balance check and overpay, or one update could be lost.
3. Currency validation was split between the endpoint (length check) and the domain (exceptions):
   whitespace returned 500, and `"123"` created an invoice the UI could not render.

## Decision

1. **Validate in the domain, before any state change.** Unit price, line total and invoice total
   are capped at `Invoice.MaxAmount` (1 billion), quantity at `MaxQuantity`, and text fields have
   maximum lengths. A rejected operation leaves the aggregate unchanged.
2. **Explicit supported-currency policy** (`Currencies.Supported`: GBP, EUR, USD). Endpoints do no
   validation of their own.
3. **`ErrorType` on every `Error`** (Validation, NotFound, Conflict, Rule). The API maps it to
   400 / 404 / 409 / 422 in one place (`ToHttp`).
4. **Optimistic concurrency at the repository boundary.** `GetAsync` returns a copy; `SaveAsync`
   returns `false` if the invoice changed since it was loaded, and the use case returns
   `409 invoice.concurrency_conflict`. The in-memory adapter stores immutable versioned snapshots;
   the EF Core adapter will use a rowversion column with the same contract.

## Consequences

- Overflow and invalid currencies become stable 400 errors with codes clients can rely on.
- Concurrent writers can't overpay; the loser gets a 409 and retries with fresh data.
- Optimistic concurrency doesn't stop duplicate submissions of the same payment. That needs
  idempotency keys (tracked in `PLAN.md`).
- The limits are product decisions. Changing them needs a test update and a note here.
- Verified by regression tests at every level, plus a mutation check: disabling the version check
  makes the stale-write and 50-concurrent-payments tests fail.
