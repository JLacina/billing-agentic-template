## What and why

<!-- One or two sentences. Link the PLAN.md item or ticket. -->

## How it was verified

- [ ] `dotnet build` and `dotnet test` green
- [ ] `web`: lint, typecheck, test:coverage green
- [ ] New or changed behaviour has a test that fails without this change

## Risk

- [ ] Touches payments, money calculation, or auth → **human security review required**
- [ ] AI-assisted: the author has read and understood every line and owns this change
