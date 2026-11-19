---
name: write-tests
description: Write or improve unit, integration and component tests for this repo (xUnit + FluentAssertions for .NET, Vitest + Testing Library for React). Use when asked to add tests, raise coverage, or reproduce a bug with a failing test.
---

# Write tests

## Pick the right level

| What you're testing | Where | Tools |
|---|---|---|
| Business rule / calculation | `tests/Billing.Domain.Tests` | xUnit, FluentAssertions, no mocks |
| Use-case orchestration | `tests/Billing.Application.Tests` | hand-rolled fakes (`FakeInvoiceRepository`, `FixedClock`) |
| HTTP contract, status codes, serialization | `tests/Billing.Api.Tests` | `WebApplicationFactory<Program>` |
| Persistence adapter (copies, stale writes, concurrent requests) | `tests/Billing.Infrastructure.Tests` | real `InMemoryInvoiceRepository`, `Task.WhenAll` for concurrency |
| React component behaviour | `web/src/**/X.test.tsx` next to the component | Vitest, Testing Library |

Test at the lowest level that can catch the bug. Don't test a domain rule through HTTP.

## Rules

- Name: `Method_Condition_ExpectedResult` (C#) or a plain sentence (`it('shows an error when…')`).
- Arrange / Act / Assert, one behaviour per test. Use `[Theory]` / `it.each` for input variations.
- Money edge cases to consider: zero, negative, rounding midpoint (x.xx5), currency mismatch,
  exact-balance payment, one penny over.
- Assert on error **codes**, not message text.
- No `Thread.Sleep`, no real clock, no real network. Inject `TimeProvider` and loader functions.
- React: query by role, label or text the user sees. Never by CSS class or test-id unless unavoidable.
- A bug fix starts with a failing test that reproduces the bug. Show it failing, then fix it.

## Finish

Run the suite you touched (`dotnet test` or `cd web && npm run test:coverage`) and report the
real pass/fail counts. Coverage must not drop.
