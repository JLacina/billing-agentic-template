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
| `docs/knowledge/` | Reference notes for future work, e.g. [managing context in AI-assisted development](docs/knowledge/managing-ai-context.md) |
| `.github/workflows/ci.yml` | Build, analyzers, format, tests + coverage, SonarQube, CodeQL, gitleaks, dependency audit |
| `Directory.Build.props`, `.editorconfig` | .NET analyzers at `latest-recommended`, warnings as errors, NuGet vulnerability audit |
| `src/` | `Domain` (rules) -> `Application` (use cases) -> `Api` (HTTP); `Infrastructure` (persistence) |
| `tests/` | Domain unit tests, application tests with fakes, API integration tests (`WebApplicationFactory`) |
| `web/` | React 19 + Vite, Vitest + Testing Library, ESLint zero-warnings, 80% coverage threshold |

## Run it

```bash
dotnet test                                               # 86 backend tests (70 unit, 16 integration)
dotnet run --project src/Billing.Api --urls http://localhost:5000

cd web && npm ci
npm run test:coverage                                     # 11 frontend tests + coverage gate
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
   - **Settings → Rules → Rulesets:** add a ruleset for `main` that requires a pull request with one
     approval and requires the CI jobs (`build`, `unit-tests`, `integration-tests`, `frontend`,
     `sonar`, `secrets`, `vulnerability-scan`) to pass.
   - In the same ruleset, enable **Require code scanning results** for the tools **CodeQL** and
     **osv-scanner**, with security alerts **High or higher** and alerts **Errors**. Without this,
     CodeQL findings are only reported, not enforced (see the `codeql` row below).
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
| **unit-tests** | Runs the Domain tests (business rules: money, currency policy, limits, invoice lifecycle) and Application tests (use cases with fakes). Collects coverage and adds a results table to the run's Summary page | Any test fails | `dotnet test tests/Billing.Domain.Tests` and `dotnet test tests/Billing.Application.Tests` |
| **integration-tests** | Runs the API tests through the real HTTP pipeline (routing, JSON, dependency injection, status codes, ProblemDetails) using `WebApplicationFactory`, and the Infrastructure tests for the repository (independent copies, stale-write rejection, 50 concurrent payments). Collects coverage and adds a results table | Any test fails | `dotnet test tests/Billing.Api.Tests` and `dotnet test tests/Billing.Infrastructure.Tests` |
| **frontend** | `npm ci --ignore-scripts`, ESLint (zero warnings), TypeScript type check, Vitest UI tests with coverage (with a results table), `npm audit`, production build | Lint warning, type error, failing test, coverage under 80%, high-severity npm vulnerability, or build error | `cd web && npm run lint && npm run typecheck && npm run test:coverage` |
| **sonar** | Sends code and coverage to SonarQube Cloud, which checks bugs, vulnerabilities, code smells, duplication and coverage on new code, then waits for the quality gate | The quality gate fails (skipped if `SONAR_TOKEN` is not set) | n/a (see the SonarQube dashboard) |
| **codeql** | GitHub's static security analysis of **our own** C# and TypeScript code (injection, XSS, unsafe data flow, and so on) with the `security-extended` queries. Findings appear under Security > Code scanning | The analysis cannot run (for example the code doesn't build). **Findings do not fail this job**; they block merges only when the ruleset requires code scanning results (see the template checklist) | n/a |
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

- FluentAssertions is pinned to 7.2.0 (Apache 2.0); version 8 and later need a paid licence for
  commercial use. `dependabot.yml` ignores its major updates so this stays enforced.
- Supported currencies are an explicit list (`Currencies.Supported`: GBP, EUR, USD). Amounts,
  quantities and invoice totals have upper limits (`Invoice.MaxAmount` and friends) so money
  arithmetic can never overflow.
- The in-memory repository uses optimistic concurrency (versioned snapshots): a write based on a
  stale read gets `409 invoice.concurrency_conflict`. EF Core will use the same pattern via rowversion.
- `web` uses Vite 8 (Rolldown), which also avoids the native Rollup binary that some locked-down
  Windows machines block.

---

## Extra: Knowledge Transfer: Managing Context in AI-Assisted Development

Effective AI-assisted development requires deliberate management of scope, vocabulary, constraints, and decisions. A continuous chat interface does not guarantee that a growing conversation remains coherent.

### Bounded contexts and shared vocabulary

In domain-driven design (DDD), a **bounded context** defines where a particular model and vocabulary apply. The same word can legitimately mean different things in different contexts:

- **Sales:** a customer is a lead with potential value.
- **Shipping:** a customer is a delivery address and order history.
- **Billing:** a customer is a payment account with invoices and credit status.

Apply the same discipline to AI conversations. Define ambiguous terms before using them, and make translations between contexts explicit.

For example:

> In this conversation, “invoice” means our domain aggregate,
> “customer” means the billing account, and “payment” means recording
> an amount received—not capturing a card transaction.

### Four common failure modes

| Failure mode | Warning sign | Recommended response |
|---|---|---|
| **Vocabulary mismatch** | The answer sounds reasonable but solves a different problem. | Ask what the assistant understood an ambiguous term to mean, then establish a shared definition. |
| **Constraint drift** | You repeatedly say, “Remember, we agreed…” | Restate the current goal and important constraints together. |
| **Mixed goals or working modes** | Responses mix explanation, brainstorming, and implementation. | Declare the intended mode and separate work with different purposes. |
| **Old assumptions surviving a pivot** | Suggestions keep returning to an abandoned approach. | Start a fresh conversation with the new direction and only the conclusions that still apply. |

These are practical diagnostic patterns. Message counts and repeated-correction thresholds are heuristics, not universal limits on model behavior.

### Six practices for maintaining useful context

1. **Start with a conversation charter.**  
   State the scope, define ambiguous vocabulary, and declare the working mode. Include important constraints before requesting substantial work.

   > We are working on invoice payment validation.
   > “Payment” means recording money already received.
   > We are exploring the existing behavior; do not change code yet.

2. **Declare the working mode.**  
   Make the expected output explicit:

   | Mode | What to request |
   |---|---|
   | Exploring | Explain the existing system, assumptions, and risks. |
   | Brainstorming | Generate alternatives for later evaluation. |
   | Deciding | Recommend an approach and explain the tradeoffs. |
   | Implementing | Execute an agreed decision within stated constraints. |

3. **Keep one conversation focused on one domain and purpose.**  
   Separate unrelated tasks. Start a fresh conversation when moving between substantial phases, such as research and implementation, to reduce carryover from earlier framing.

4. **Transfer conclusions deliberately.**  
   Begin the next conversation with a short handoff containing accepted decisions, relevant constraints, and the next objective. Leave behind abandoned alternatives and unnecessary conversation history.

   > Previous work established:
   > - Payment rules belong in the Invoice aggregate.
   > - Overpayments must return a stable business error.
   > - Persistence changes are outside this task.
   > Next: implement the rule and verify it with tests.

5. **Recognize when repeated correction needs a new boundary.**  
   If the same misunderstanding survives several corrections, reassess the conversation’s framing. A fresh context can prevent old assumptions from shaping the new task. This mirrors DDD’s anti-corruption-layer principle: protect one model from assumptions belonging to another.

6. **Periodically re-anchor long conversations.**  
   Restate the current goal, accepted decisions, and constraints. Every 15–20 messages is a practical reminder, or earlier when drift appears. Also check whether those constraints still reflect what you want.

### Match the task to the amount of context management it needs

Two questions help determine how much structure a task needs:

1. How clearly is the domain defined?
2. How clearly can success be evaluated?

| Domain clarity | Success criteria | Suggested approach |
|---|---|---|
| Clear | Clear | Use a focused request and verify the result. Examples: function conversion, regex matching, or formatting. |
| Unclear | Clear | Clarify the domain first; an answer can look correct while relying on wrong assumptions. |
| Clear | Unclear | Define evaluation criteria and iterate. Examples: refactoring and code review. |
| Unclear | Unclear | Separate discovery, decisions, and implementation. Architecture work needs especially deliberate context management. |

Small, self-contained tasks usually need less conversational setup. Broader tasks benefit from explicit boundaries and carefully chosen handoffs.

### Applying the lessons in this repository

- Use `CLAUDE.md` for standing architecture rules, commands, and constraints.
- Use `PLAN.md` for task scope, acceptance criteria, and progress.
- Use `docs/adr/` for durable decisions and the rationale future maintainers need.
- Use focused skills for repeatable procedures.
- Start implementation sessions with the relevant decisions and constraints.
- Verify results through tests, automated checks, and human review.

Keep conversation handoffs concise while preserving durable reasoning in project documentation. The objective is to carry forward the context needed for the next task and make its assumptions explicit.
