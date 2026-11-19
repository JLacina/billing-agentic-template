// Mirrors Billing.Application.InvoiceDto. Keep in sync with the API contract.
export type InvoiceStatus = 'Draft' | 'Issued' | 'PartiallyPaid' | 'Paid'

export interface InvoiceLine {
  description: string
  quantity: number
  unitPrice: number
  taxRate: number
  gross: number
}

export interface Invoice {
  id: string
  customerId: string
  currency: string
  status: InvoiceStatus
  issuedOn: string | null
  total: number
  amountPaid: number
  outstanding: number
  lines: InvoiceLine[]
}
