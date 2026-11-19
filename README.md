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
dotnet test                                               # 34 backend tests (29 unit, 5 integration)
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

## CI pipeline explained

Every push to `main` and every pull request runs `.github/workflows/ci.yml`. Each job is one box
in the GitHub Actions diagram. Most run in parallel; `sonar` waits for the test jobs because it
needs their coverage reports.

```
build ─────────────┐
unit-tests ────────┤
integration-tests ─┼──► sonar (quality gate)
frontend ──────────┘
codeql (C#, TS)   secrets   vulnerability-scan   dependency-review (PRs only)
```

| Job | What it does | Fails when | Run it locally |
|---|---|---|---|
| **build** | Restores NuGet packages from the lock files, compiles with analyzers at `latest-recommended` and warnings as errors, checks formatting, lists vulnerable packages | Compile error, any analyzer warning, unformatted code, a vulnerable NuGet package, or lock files out of date | `dotnet build` and `dotnet format --verify-no-changes` |
| **unit-tests** | Runs the Domain tests (business rules: money, tax, invoice lifecycle) and Application tests (use cases with fakes). Collects coverage and adds a results table to the run's Summary page | Any test fails | `dotnet test tests/Billing.Domain.Tests` and `dotnet test tests/Billing.Application.Tests` |
| **integration-tests** | Runs the API tests through the real HTTP pipeline (routing, JSON, dependency injection, status codes, ProblemDetails) using `WebApplicationFactory`. Collects coverage and adds a results table | Any test fails | `dotnet test tests/Billing.Api.Tests` |
| **frontend** | `npm ci --ignore-scripts`, ESLint (zero warnings), TypeScript type check, Vitest UI tests with coverage (with a results table), `npm audit`, production build | Lint warning, type error, failing test, coverage under 80%, high-severity npm vulnerability, or build error | `cd web && npm run lint && npm run typecheck && npm run test:coverage` |
| **sonar** | Sends code and coverage to SonarQube Cloud, which checks bugs, vulnerabilities, code smells, duplication and coverage on new code, then waits for the quality gate | The quality gate fails (skipped if `SONAR_TOKEN` is not set) | n/a (see the SonarQube dashboard) |
| **codeql** | GitHub's static security analysis of **our own** C# and TypeScript code (injection, XSS, unsafe data flow, and so on) with the `security-extended` queries. Findings appear under Security > Code scanning | A security issue is found | n/a |
| **secrets** | gitleaks scans the full git history for committed passwords, API keys and tokens | A secret is found | `gitleaks detect` (if installed) |
| **vulnerability-scan** | OSV-Scanner checks every NuGet and npm dependency, including indirect ones, against the OSV vulnerability database. Findings appear under Security > Code scanning | A dependency has a known vulnerability | `osv-scanner -r .` (if installed) |
| **dependency-review** | On pull requests only: blocks a PR that **adds** a dependency with a high-severity vulnerability | The PR introduces a high-severity vulnerable package | n/a |

<img width="1364" height="796" alt="image" src="https://github.com/user-attachments/assets/8e861cf9-1696-4773-aad5-4f53dfb9514a" />


**Where to see results:** open a run under **Actions**, then:
- the **Summary** page shows the test result tables from `unit-tests`, `integration-tests` and `frontend`;
- click a job, then a step, to see its full log;
- security findings are under **Security → Code scanning**, quality findings on the SonarQube dashboard.

**Other automation:** Dependabot (`.github/dependabot.yml`) opens weekly PRs to update NuGet, npm and
GitHub Actions versions. Each of those PRs goes through the same CI jobs before it can be merged.

**Repository settings these jobs rely on:**
- `sonar`: secret `SONAR_TOKEN`, variables `SONAR_ORG` and `SONAR_PROJECT_KEY`, and Automatic Analysis
  switched **off** in SonarQube Cloud (it rejects CI scans while Automatic Analysis is on).
- `dependency-review`: the Dependency graph enabled (Settings → Security) and the variable
  `DEPENDENCY_GRAPH_ENABLED` set to `true`.

## Setting up SonarQube

Create a SonarQube Cloud project, then in GitHub add the `SONAR_TOKEN` secret and the
`SONAR_PROJECT_KEY` and `SONAR_ORG` variables. The `sonar` job is skipped until the token exists.
Use the "Sonar way" quality gate (no new bugs or vulnerabilities, at least 80% coverage on new code,
no more than 3% duplication).

## Notes

- FluentAssertions is pinned to 7.x (Apache 2.0); version 8 and later need a commercial licence.
- `web` uses Vite 8 (Rolldown), which also avoids the native Rollup binary that some locked-down
  Windows machines block.
