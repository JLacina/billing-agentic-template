# Billing: agentic engineering template

A small but production-shaped billing service (.NET 10 + React/TypeScript) set up so that
AI coding agents can work on it **safely and repeatably**: written standards, a spec, reusable
skills, and automated quality gates that every change, human or AI, has to pass.

## What's here

| Path | Purpose |
|---|---|
| `CLAUDE.md` | Standing instructions for the agent: commands, architecture rules, definition of done, security rules |
| `PLAN.md` | Feature spec and backlog with acceptance criteria; the agent works from it and ticks items off |
| `.claude/skills/` | Reusable procedures: `add-api-endpoint`, `write-tests`, `pre-pr-check`, `billing-security-check` |
| `.claude/settings.json` | Agent permissions: safe commands allowed, commits/installs need approval, secrets unreadable |
| `docs/adr/` | Architecture Decision Records: conclusions carried between sessions and people |
| `.github/workflows/ci.yml` | Build, analyzers, format, tests + coverage, SonarQube, CodeQL, gitleaks, dependency audit |
| `Directory.Build.props`, `.editorconfig` | .NET analyzers at `latest-recommended`, warnings as errors, NuGet vulnerability audit |
| `src/` | `Domain` (rules) -> `Application` (use cases) -> `Api` (HTTP); `Infrastructure` (persistence) |
| `tests/` | Domain unit tests, application tests with fakes, API integration tests (`WebApplicationFactory`) |
| `web/` | React 19 + Vite, Vitest + Testing Library, ESLint zero-warnings, 80% coverage threshold |

## Run it

```bash
dotnet test                                               # 34 backend tests
dotnet run --project src/Billing.Api --urls http://localhost:5000

cd web && npm ci
npm run test:coverage                                     # 8 frontend tests + coverage gate
npm run dev                                               # http://localhost:5173
```

## The workflow

1. **Spec:** add or refine an item in `PLAN.md` with acceptance criteria.
2. **Plan:** ask the agent to propose an approach; approve it.
3. **Build:** the agent uses a skill (e.g. `/add-api-endpoint`), writing tests first.
4. **Verify:** `/pre-pr-check` runs every gate locally and reviews the diff against `CLAUDE.md`.
5. **Secure:** `/billing-security-check` for anything touching money or auth.
6. **Human gate:** a person reviews and approves; CI enforces the same gates on the PR.
7. **Measure:** track lead time, rework, review effort and escaped defects to see whether it helps.

## Setting up SonarQube

Create a SonarQube Cloud project, then in GitHub add the `SONAR_TOKEN` secret and the
`SONAR_PROJECT_KEY` and `SONAR_ORG` variables. The `sonar` job is skipped until the token exists.
Use the "Sonar way" quality gate (no new bugs or vulnerabilities, at least 80% coverage on new code,
no more than 3% duplication).

## Notes

- FluentAssertions is pinned to 7.x (Apache 2.0); version 8 and later need a commercial licence.
- `web` uses Vite 8 (Rolldown), which also avoids the native Rollup binary that some locked-down
  Windows machines block.
