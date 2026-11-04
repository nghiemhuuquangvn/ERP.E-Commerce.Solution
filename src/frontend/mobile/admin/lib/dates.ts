import { format, parseISO } from 'date-fns'

/** yyyy-MM-dd for a server date / date-time; empty text for a missing or unreadable value (never "Invalid Date"). */
export function formatDate(value?: string | null): string {
  if (!value) return ''
  try {
    return format(parseISO(value), 'yyyy-MM-dd')
  } catch {
    return ''
  }
}

/** ISO date (yyyy-MM-dd) `days` days before today, for report ranges. */
export function daysAgo(days: number, now = new Date()): string {
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - days)
  return format(d, 'yyyy-MM-dd')
}
