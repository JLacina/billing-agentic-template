# Billing service: agent instructions

Invoicing and payments for a hospitality / point-of-sale platform.
Backend: .NET 10 minimal API (Clean Architecture). Frontend: React 19 + TypeScript (Vite).
Feature scope and status live in `PLAN.md`. Design decisions live in `docs/adr/`.

## Commands

| Task | Command (run from repo root unless noted) |
|---|---|
| Build backend (warnings = errors) | `dotnet build` |
| Backend tests | `dotnet test` |
| Backend coverage | `dotnet test --collect:"XPlat Code Coverage"` |
| Format check | `dotnet format --verify-no-changes` |
| Frontend install | `cd web && npm ci` |
| Frontend lint / types / tests | `cd web && npm run lint && npm run typecheck && npm run test:coverage` |
| Run API | `dotnet run --project src/Billing.Api --urls http://localhost:5000` |
| Run web (proxies /api to :5000) | `cd web && npm run dev` |

## Architecture rules (enforced in review; break them only with an ADR)

- Dependencies point inward: `Api -> Application -> Domain`, `Infrastructure -> Application`.
  `Domain` references nothing. `Application` never references `Infrastructure` or ASP.NET.
- Business rules live in the domain aggregate (`Invoice`), never in endpoints or services.
  Services orchestrate: load, call domain, save.
- Expected business failures return `Result` / `Error` with a stable `code` and an `ErrorType`.
  Exceptions are for bugs. Validate in the domain, before any state changes.
- Endpoints map `Result` to HTTP in one place (`ToHttp`) by `ErrorType`: Validation 400,
  NotFound 404, Conflict 409, Rule 422. Endpoints don't validate. Responses are RFC 7807 ProblemDetails.
- Any user-supplied number has an upper bound (`Invoice.MaxAmount`, `MaxQuantity`); currencies come
  from the explicit `Currencies.Supported` list.
- Repositories return copies and save with optimistic concurrency (`SaveAsync` returns `false` on a
  stale write). Never rely on shared mutable entities between requests.
- Money is always `Money` (decimal + ISO currency). Never `double`/`float`, never a bare decimal
  crossing the domain boundary.
- Time comes from the injected `TimeProvider`, never `DateTime.Now`, so it can be tested.
- Frontend: feature folders (`src/features/<feature>`), shared helpers in `src/shared`.
  Components receive data loaders as props so they are testable without the network.

## Coding standards

- SOLID and DRY, but prefer the simplest design that meets the requirement; no speculative abstractions.
- Small, intention-revealing names. Keep methods short. No commented-out code.
- C#: nullable on, file-scoped namespaces, `sealed` by default, analyzers at `latest-recommended`.
  Fix analyzer warnings; don't suppress them without a one-line justification.
- C# documentation: every public type and member in `src/` has XML docs (the build fails otherwise).
  Methods get `<summary>` (what it is meant to do), `<param>` for each parameter, `<returns>` (including
  which errors it can return) and `<exception>` where it throws; use `<remarks>` for the why.
  Keep `docs/code-walkthrough.md` up to date when a class's responsibility changes.
- C# async: async methods end in `Async` and take a `CancellationToken` as the last parameter, passed
  all the way down (endpoint -> service -> repository). Never block with `.Result`, `.Wait()` or
  `GetAwaiter().GetResult()`.
- C# immutability: value objects and request/response DTOs are `sealed record`s. Return collections
  as `IReadOnlyList<T>` / `IReadOnlyCollection<T>`. The aggregate has no public setters; state
  changes only through its methods.
- API contracts: endpoints accept and return DTOs (`InvoiceDtos.cs`), never domain types, so a domain
  refactor can't silently change the public API.
- Logging: use `ILogger` with structured message templates (`"Invoice {InvoiceId} paid"`), not string
  interpolation. Log identifiers, never whole requests, DTOs or entities (they can carry sensitive data).
- Tests: name them `Method_Scenario_ExpectedResult` (e.g. `Pay_AmountExceedsBalance_ReturnsRuleError`),
  structure them Arrange / Act / Assert, and test one behaviour per test.
- TypeScript: `strict` + `noUncheckedIndexedAccess`. No `any`. ESLint must pass with zero warnings.
- TypeScript documentation: every exported function, component and type in `web/src` has a JSDoc
  comment: a one-line summary, `@param` for each parameter (for components, document the props
  type's fields), and `@returns` where the return isn't obvious, including which errors it can throw.
- React: every component that loads data renders explicit loading, error and empty states. Effects
  clean up (abort fetches, remove listeners). Interactive elements have accessible names (visible
  label or `aria-label`) so tests can query by role.

## Definition of done (every change)

1. A test that fails without the change and passes with it (write it first when practical).
   - Domain rule -> `tests/Billing.Domain.Tests`
   - Use-case orchestration -> `tests/Billing.Application.Tests` (hand-rolled fakes)
   - HTTP contract / status codes -> `tests/Billing.Api.Tests` (`WebApplicationFactory`)
   - Repository behaviour (copies, optimistic concurrency) -> `tests/Billing.Infrastructure.Tests`
   - UI behaviour -> co-located `*.test.tsx` with Testing Library (query by role/label, not CSS)
2. `dotnet build`, `dotnet test`, and the frontend lint/typecheck/test commands are all green.
   Run them; don't claim success from reading the diff.
3. Coverage does not drop (frontend threshold 80% is enforced in `vite.config.ts`).
4. `PLAN.md` checkbox updated. Add an ADR if you made an architectural decision.
5. If a test fails for a reason unrelated to your change, report it; don't work around it silently.

## Security rules (billing = high risk)

- Never log or persist card numbers, CVV, bank details or secrets. We don't store PAN data at all.
- Never commit secrets. Configuration comes from environment variables / user-secrets.
- Treat all client input as untrusted (amounts, currency, status). Enforce business validation in the
  domain before mutation; map validation failures to HTTP responses at the API boundary.
- Any change to authentication, authorisation, payments or money calculation needs human review.
  Say so explicitly in your summary.
- New dependencies: prefer well-maintained packages, pin versions, and run `npm audit` /
  `dotnet list package --vulnerable` before proposing them.

## Agent workflow

- For anything larger than a small fix: read `PLAN.md`, propose a short plan, wait for approval.
- Use the project skills in `.claude/skills/` (`/add-api-endpoint`, `/write-tests`, `/pre-pr-check`, `/new-project-setup`,
  `/billing-security-check`).
- Never commit, push, or open a PR unless explicitly asked each time.
- When handing off, write conclusions (decisions, open questions) to `docs/adr/` or `PLAN.md`,
  not a transcript.
