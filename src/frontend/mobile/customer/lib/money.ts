/**
 * The app's one money formatter (US dollars, as before U5). Screens call this instead of building their own
 * Intl.NumberFormat, so the shared money / date rule (U6) can replace it in one place.
 */
const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export const formatMoney = (n: number) => usd.format(n)

export function formatOrderMoney(n: number, currency = 'USD') {
  return new Intl.NumberFormat(currency === 'VND' ? 'vi-VN' : 'en-US', {
    style: 'currency',
    currency,
    maximumFractionDigits: currency === 'VND' ? 0 : 2,
  }).format(n)
}
