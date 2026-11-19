import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import { InvoiceList } from './InvoiceList'
import type { Invoice } from './types'

const invoice = (overrides: Partial<Invoice> = {}): Invoice => ({
  id: '1',
  customerId: 'stadium-north',
  currency: 'GBP',
  status: 'Issued',
  issuedOn: '2026-03-01',
  total: 120,
  amountPaid: 20,
  outstanding: 100,
  lines: [],
  ...overrides,
})

describe('InvoiceList', () => {
  it('shows a loading state first', () => {
    render(<InvoiceList loadInvoices={() => new Promise(() => {})} />)
    expect(screen.getByRole('status')).toHaveTextContent('Loading invoices')
  })

  it('renders invoices with formatted money', async () => {
    render(<InvoiceList loadInvoices={() => Promise.resolve([invoice()])} />)

    expect(await screen.findByRole('table', { name: 'Invoices' })).toBeInTheDocument()
    expect(screen.getByText('stadium-north')).toBeInTheDocument()
    expect(screen.getByText('£120.00')).toBeInTheDocument()
    expect(screen.getByText('£100.00')).toBeInTheDocument()
  })

  it('shows an empty state', async () => {
    render(<InvoiceList loadInvoices={() => Promise.resolve([])} />)
    expect(await screen.findByText('No invoices yet.')).toBeInTheDocument()
  })

  it('shows the error message when loading fails', async () => {
    render(<InvoiceList loadInvoices={() => Promise.reject(new Error('HTTP 500'))} />)
    expect(await screen.findByRole('alert')).toHaveTextContent('HTTP 500')
  })
})
