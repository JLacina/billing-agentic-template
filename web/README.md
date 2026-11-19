# Billing web

React 19 + TypeScript frontend for the billing API, built with Vite.

```bash
npm ci --ignore-scripts
npm run dev            # http://localhost:5173, proxies /api to http://localhost:5000
npm run lint           # ESLint, zero warnings allowed
npm run typecheck      # tsc in strict mode with noUncheckedIndexedAccess
npm run test:coverage  # Vitest + Testing Library, fails under 80% coverage
npm run build          # production build into dist/
```

Structure:

- `src/features/<feature>/`: components, API client, types and co-located tests for one feature
- `src/shared/`: helpers used across features (e.g. `formatMoney`)
- `src/test/setup.ts`: Testing Library matchers and cleanup

Components receive data loaders as props (e.g. `InvoiceList loadInvoices={fetchInvoices}`), so tests
run without a network. See the root `README.md` and `CLAUDE.md` for the full conventions.
