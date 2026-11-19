# ADR 0001: Clean Architecture layers and the Result pattern

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

Billing rules (tax, rounding, payment state) must be correct, easy to test, and safe for both
engineers and AI agents to change. Rules scattered across controllers, services and SQL are the
usual source of billing defects.

## Decision

1. Four projects with inward-pointing dependencies: `Domain` <- `Application` <- `Api`, and
   `Infrastructure` -> `Application`. The domain has no framework dependencies.
2. The `Invoice` aggregate owns every state transition and invariant.
3. Expected business failures are returned as `Result`/`Error` with a stable code, not thrown.
   The API maps them to ProblemDetails in a single function.
4. Money is a `Money` value object (decimal, ISO currency, banker's rounding).

## Consequences

- Rules are unit-tested in milliseconds with no mocks; agents get one obvious place to put logic.
- Swapping the in-memory repository for EF Core touches only `Infrastructure`.
- Slightly more ceremony than a single-project API; acceptable for a money-handling service.
- Error codes are part of the API contract and must not be renamed casually.
