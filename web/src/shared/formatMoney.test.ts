import { describe, expect, it } from 'vitest'
import { formatMoney } from './formatMoney'

describe('formatMoney', () => {
  it('formats GBP with symbol and two decimals', () => {
    expect(formatMoney(1234.5, 'GBP')).toBe('£1,234.50')
  })

  it('formats other currencies', () => {
    expect(formatMoney(10, 'EUR')).toBe('€10.00')
  })

  // Regression: Intl throws RangeError for codes like "123", which used to break the invoice list.
  it.each(['123', '', 'NOT-A-CODE'])('falls back to plain text for invalid currency %j', (currency) => {
    expect(formatMoney(12.5, currency)).toBe(`12.50 ${currency}`)
  })
})
