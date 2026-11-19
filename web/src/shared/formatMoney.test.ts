import { describe, expect, it } from 'vitest'
import { formatMoney } from './formatMoney'

describe('formatMoney', () => {
  it('formats GBP with symbol and two decimals', () => {
    expect(formatMoney(1234.5, 'GBP')).toBe('£1,234.50')
  })

  it('formats other currencies', () => {
    expect(formatMoney(10, 'EUR')).toBe('€10.00')
  })
})
