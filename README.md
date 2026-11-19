# Billing: agentic engineering template

A small but production-shaped billing service (.NET 10 + React/TypeScript) set up so that
AI coding agents can work on it **safely and repeatably**: written standards, a spec, reusable
skills, and automated quality gates that every change, human or AI, has to pass.

## What's here

| Path | Purpose |
|---|---|
| `CLAUDE.md` | Standing instructions for the agent: commands, architecture rules, definition of done, security rules |
| `PLAN.md` | Feature spec and backlog with acceptance criteria; the agent works from it and ticks items off |
| `.claude/skills/` | Reusable procedures: `add-api-endpoint`, `write-tests`, `pre-pr-check`, `billing-security-check`, `new-project-setup` |
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

## Use this as a template for a new project

The billing code is a worked example. What you reuse is the **structure around it**: the agent
instructions, skills, quality gates and CI. Replace the domain with your own.

### 1. Create the new repository

- One-time, on this repo: **Settings → General → tick "Template repository"**.
- Then click **Use this template → Create a new repository** (or `gh repo create my-app --template JLacina/billing-agentic-template`).
- Clone it and open it in your editor with Claude Code.

### 2. Let Claude do the setup

Run the project skill and answer its questions:

```
/new-project-setup
```

It asks for the product name, domain and first feature, then renames the solution, projects and
namespaces (`Billing.*` → `YourProduct.*`), rewrites `CLAUDE.md`, `PLAN.md` and the domain security
skill for your domain, and runs every gate to prove the renamed project still builds and passes.
You can also follow the manual checklist below.

### 3. What to keep, change or replace

| Keep as is (the reusable part) | Adapt to your project | Replace (example code) |
|---|---|---|
| `Directory.Build.props`, `.editorconfig` (analyzers, warnings as errors, NuGet audit, lock files) | `CLAUDE.md`: product description, domain rules, security rules for your risk areas | `src/*/Invoices/*`, `Money`: your own aggregates and value objects |
| `.github/workflows/ci.yml`, `dependabot.yml`, PR template | `PLAN.md`: your goal, scope and first backlog items with acceptance criteria | `tests/**`: tests for your domain (keep the three-level structure) |
| `Result`/`Error` pattern, `ToHttp` mapping, layer structure | `.claude/skills/billing-security-check` → rename to `<domain>-security-check` with your risks | `web/src/features/invoices`: your first feature |
| Skills `add-api-endpoint`, `write-tests`, `pre-pr-check` (update names and paths) | `.claude/settings.json`: add your own allowed and denied commands | `docs/adr/0001`: keep it as an example or rewrite it for your decisions |

### 4. Manual checklist (if you don't use the skill)

1. Rename: `Billing.sln`, `src/Billing.*`, `tests/Billing.*`, and every `Billing` namespace and
   project reference to your product name. Then run `dotnet build` and `dotnet test`.
2. Delete the invoice example code and its tests, keeping `Common/Result.cs` and the project structure.
3. Rewrite `CLAUDE.md` (keep the sections: commands, architecture rules, coding standards,
   definition of done, security rules, agent workflow) and `PLAN.md`.
4. Regenerate the NuGet lock files with `dotnet restore --force-evaluate`, and commit them.
5. Set up the repository on GitHub:
   - **Secrets and variables → Actions:** `SONAR_TOKEN`, `SONAR_ORG`, `SONAR_PROJECT_KEY` (and turn off
     Automatic Analysis in SonarQube Cloud).
   - **Settings → Security:** enable the Dependency graph and Dependabot alerts, then set the
     variable `DEPENDENCY_GRAPH_ENABLED` to `true`.
   - **Settings → Branches:** protect `main`, require a pull request with one approval, and require
     the CI jobs (`build`, `unit-tests`, `integration-tests`, `frontend`, `sonar`, `codeql`,
     `secrets`, `vulnerability-scan`) to pass.
6. Push, and check that every CI job is green before building features.

### 5. Start building

Give the agent a feature from `PLAN.md`, for example:

```
Implement the first unchecked item in PLAN.md. Propose a plan first and wait for my approval.
Use /add-api-endpoint, write the tests first, and finish with /pre-pr-check.
```

Review the plan, review the diff, open a PR, and let CI and a human reviewer decide whether it merges.

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
