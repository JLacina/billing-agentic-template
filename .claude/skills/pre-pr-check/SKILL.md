---
name: pre-pr-check
description: Run every local quality gate (build, analyzers, format, tests, coverage, lint, type check, dependency audit) and review the diff against CLAUDE.md before a pull request. Use when asked if a change is ready, before opening a PR, or to "check everything".
---

# Pre-PR check

Run each gate and record the real result. Stop and report at the first failure you can't fix
safely. Never mark a gate as passed without running it.

## 1. Backend

```bash
dotnet build                          # analyzers + warnings-as-errors + NuGet audit
dotnet format --verify-no-changes
dotnet test --collect:"XPlat Code Coverage"
dotnet list package --vulnerable --include-transitive
```

## 2. Frontend

```bash
cd web
npm ci --ignore-scripts
npm run lint          # zero warnings allowed
npm run typecheck
npm run test:coverage # fails under the 80% threshold
npm audit --audit-level=high
```

## 3. Review the diff (`git diff main...HEAD`)

Check against `CLAUDE.md`:
- Dependency direction respected (Domain references nothing; no ASP.NET in Application)
- Business rules only in the domain; endpoints only map
- Every behaviour change has a test; no tests deleted or weakened to make the build pass
- No secrets, no PII or card data in logs, no `double` for money, no `DateTime.Now`
- `PLAN.md` updated; ADR added if architecture changed

## 4. Report

Output a table: gate | result | notes. Then list any review findings, most severe first.
Explicitly flag changes to payments, money calculation or auth as **needs human review**.
Do not commit or push.
