import type { Invoice } from './types'

/** Thin API client. Components depend on this function's shape, not on fetch (easy to fake in tests). */
export type FetchInvoices = () => Promise<Invoice[]>

export const fetchInvoices: FetchInvoices = async () => {
  const response = await fetch('/api/invoices')
  if (!response.ok) {
    throw new Error(`Failed to load invoices (HTTP ${response.status})`)
  }
  return (await response.json()) as Invoice[]
}
