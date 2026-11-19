# Plan: Billing service

**Goal:** Operators can raise invoices for venue customers (corporate boxes, concessions,
season packages), issue them, and record payments, with correct tax and money handling.

**Out of scope (for now):** card capture (handled by the payment provider), multi-currency
conversion, credit notes.

## Done

- [x] Domain: `Money` value object (decimal, ISO currency, banker's rounding)
- [x] Domain: `Invoice` aggregate with Draft -> Issued -> PartiallyPaid -> Paid lifecycle
- [x] Rules: no edits after issue, no issuing an empty invoice, no overpayment, currency must match
- [x] API: create / list / get / add line / issue / record payment, ProblemDetails errors
- [x] Web: invoice list with loading, empty and error states
- [x] Quality gates: analyzers-as-errors, NuGet audit, ESLint zero-warnings, coverage threshold, CI

## Next (in priority order)

- [ ] **Persistence:** replace `InMemoryInvoiceRepository` with EF Core + SQL Server.
  - Acceptance: API integration tests run against a Testcontainers SQL Server; no domain changes needed.
  - Open question: optimistic concurrency via rowversion? (Two operators paying the same invoice.)
- [ ] **AuthN/AuthZ:** JWT bearer auth; `billing:read` and `billing:write` policies.
  - Acceptance: unauthenticated -> 401, wrong scope -> 403, tests for both on every endpoint.
  - Needs human security review before merge.
- [ ] **Idempotent payments:** `Idempotency-Key` header on `POST /payments`.
  - Acceptance: replaying the same key returns the original result and does not double-record.
- [ ] **Void invoice:** issued and unpaid invoices can be voided with a reason.
- [ ] **Web: invoice detail + record payment form** with client-side validation that mirrors API rules.
- [ ] **Observability:** structured logging (no PII), OpenTelemetry traces, `/health` readiness check for DB.

## Stretch

- [ ] MCP server exposing read-only invoice queries to internal agents (e.g. support triage).
- [ ] Playwright end-to-end test: create -> issue -> pay through the UI.

## Open questions

- Tax: single rate per line is enough for UK VAT; do we need tax-inclusive pricing for concessions?
- Rounding: round per line (current) or on the invoice total? Finance to confirm.
