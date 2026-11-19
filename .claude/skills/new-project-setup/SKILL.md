---
name: new-project-setup
description: Turn a fresh copy of this template into a new project - rename Billing to the new product name, rewrite CLAUDE.md, PLAN.md and the domain security skill, optionally remove the invoice example, and prove everything still builds and passes. Use right after creating a repository from this template, or when asked to "set up", "rename" or "bootstrap" the project.
---

# New project setup

Work through these steps in order. Ask before deleting anything.

## 1. Gather inputs (ask the user, one message)

- Product name in PascalCase, used for the solution and namespaces (e.g. `Ordering`, `Loyalty`)
- One-sentence description of the product and its users
- Main domain concepts (e.g. Order, Basket, Venue)
- Highest-risk areas (e.g. payments, personal data, stock levels): these drive the security rules
- First 2-4 features for `PLAN.md`
- Keep the invoice example as reference code, or remove it?

## 2. Rename (mechanical, then verify)

1. List what will change first and show the user: `git grep -n "Billing"` and `git grep -in "billing"`.
2. Rename the folders and files `Billing.sln`, `src/Billing.*`, `tests/Billing.*` with `git mv`
   so history is kept.
3. Replace `Billing` with the new name in `*.cs`, `*.csproj`, `*.sln`, `*.json`, `*.yml`, `*.md`.
   Leave `node_modules`, `bin`, `obj` and lock-file hashes alone.
4. Rename `.claude/skills/billing-security-check` to `<product-lowercase>-security-check` and update
   its `name:` and the references in `CLAUDE.md` and `README.md`.
5. Run `dotnet restore --force-evaluate`, then `dotnet build` and `dotnet test`. Fix anything the
   rename broke before continuing.

## 3. Remove the example (only if the user said so)

- Delete `src/*/Invoices/`, the invoice DTOs, service, endpoints and repository, and their tests.
- Keep `Domain/Common/Result.cs` (and `Money.cs` if the product handles money), the layer projects,
  `Program.cs` wiring, `/health`, and one passing test per test project so CI stays meaningful.
- Delete `web/src/features/invoices` and leave `App.tsx` rendering a placeholder with one test.

## 4. Rewrite the agent context

- `CLAUDE.md`: keep every section heading. Update the product description, the domain rules
  (replace invoice and money rules with the user's concepts), and the security rules (from the
  risk areas). Keep the definition of done and the agent workflow rules unchanged.
- `PLAN.md`: goal, out of scope, and the user's first features, each with acceptance criteria and
  open questions. Remove the billing items.
- The domain security skill: replace the money-integrity checklist with checks for the user's risks.
- `README.md`: update the title and description; keep the template, CI and workflow sections.
- `docs/adr/`: add `0002-<product>-initial-scope.md` recording the decisions made in this setup.

## 5. Verify and report

Run every gate from `/pre-pr-check` (backend and frontend). Then report:
- a table: gate | result
- the files renamed, deleted and rewritten
- the GitHub settings the user still has to configure (Sonar secret and variables, Dependency graph,
  branch protection), as listed in README "Use this as a template"

Do not commit or push unless the user asks.
