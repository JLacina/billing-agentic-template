import { useEffect, useState } from 'react'
import { formatMoney } from '../../shared/formatMoney'
import type { FetchInvoices } from './invoicesApi'
import type { Invoice } from './types'

type State =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'loaded'; invoices: Invoice[] }

interface Props {
  /** Injected so tests (and stories) can supply data without a network. */
  loadInvoices: FetchInvoices
}

export function InvoiceList({ loadInvoices }: Props) {
  const [state, setState] = useState<State>({ kind: 'loading' })

  useEffect(() => {
    let cancelled = false
    loadInvoices()
      .then((invoices) => { if (!cancelled) setState({ kind: 'loaded', invoices }) })
      .catch((e: unknown) => {
        if (!cancelled) setState({ kind: 'error', message: e instanceof Error ? e.message : 'Unknown error' })
      })
    return () => { cancelled = true }
  }, [loadInvoices])

  if (state.kind === 'loading') return <p role="status">Loading invoices…</p>
  if (state.kind === 'error') return <p role="alert">{state.message}</p>
  if (state.invoices.length === 0) return <p>No invoices yet.</p>

  return (
    <table>
      <caption>Invoices</caption>
      <thead>
        <tr>
          <th scope="col">Customer</th>
          <th scope="col">Status</th>
          <th scope="col">Total</th>
          <th scope="col">Outstanding</th>
        </tr>
      </thead>
      <tbody>
        {state.invoices.map((inv) => (
          <tr key={inv.id}>
            <td>{inv.customerId}</td>
            <td>{inv.status}</td>
            <td>{formatMoney(inv.total, inv.currency)}</td>
            <td>{formatMoney(inv.outstanding, inv.currency)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
