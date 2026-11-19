/** Single place for money formatting (DRY). Uses Intl so currency symbols and separators are correct. */
export function formatMoney(amount: number, currency: string, locale = 'en-GB'): string {
  return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
}
