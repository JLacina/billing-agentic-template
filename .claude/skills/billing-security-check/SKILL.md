---
name: billing-security-check
description: Security review focused on billing and payment risks (OWASP API Top 10, money integrity, PII/PCI data handling, secrets, dependencies). Use before merging changes to payments, invoices, auth, or configuration, or when asked for a security review.
---

# Billing security check

Review the current diff (or the files named by the user). For each finding give:
severity (Critical/High/Medium/Low), file:line, the concrete exploit or failure scenario, and a fix.
Report only real issues with a plausible scenario; no generic advice.

## Money integrity
- Can a client set amounts, totals, status or currency that the server should compute?
- Overpayment, negative amounts, zero amounts, rounding exploits, currency mismatch
- Double-processing: is the operation idempotent? Race between two concurrent payments?

## Access control (OWASP API1/API5)
- Can user A read or modify user B's invoice by changing the id? (BOLA / IDOR)
- Is every state-changing endpoint behind the right authorisation policy?

## Input and output
- All request fields validated at the boundary (length, range, format)
- Errors return ProblemDetails without stack traces or internal details
- No mass assignment: request DTOs contain only fields the client may set

## Sensitive data
- No card numbers, CVV, bank details, tokens or secrets in code, logs, exceptions or test data
- Config secrets come from environment / user-secrets / a vault, never `appsettings.json`

## Dependencies and supply chain
- Run `dotnet list package --vulnerable --include-transitive` and `cd web && npm audit`
- New packages: maintained, pinned, licence acceptable

## Output
A findings table, most severe first, then "No issues found in: …" for the areas you checked.
Anything touching payments or auth ends with: **Requires human security sign-off before merge.**
