/**
 * The shop's two money formats, in one place (U6 will replace the rule with one formatter per the content standard).
 * Today: plain amounts are shown as US dollars, and as Vietnamese dong once the customer has chosen a method that settles in VND
 * (checkout) or when the API says the currency is VND (payment result).
 */
export function formatUsd(n: number): string {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
}

export function formatVnd(n: number): string {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n)
}

export function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat(currency === 'VND' ? 'vi-VN' : 'en-US', {
    style: 'currency',
    currency,
    maximumFractionDigits: currency === 'VND' ? 0 : 2,
  }).format(amount)
}

export function useMoney() {
  return { usd: formatUsd, vnd: formatVnd, money: formatMoney }
}
