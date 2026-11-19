/**
 * Single place for money formatting (DRY). Uses Intl so currency symbols and separators are correct.
 * Never throws: an unknown currency code falls back to "1234.50 XXX" so one bad record can't break a page.
 */
export function formatMoney(amount: number, currency: string, locale = 'en-GB'): string {
  try {
    return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount.toFixed(2)} ${currency}`
  }
}
