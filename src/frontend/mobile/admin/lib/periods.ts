import { addMonths, addQuarters, addYears, endOfMonth, endOfQuarter, endOfYear, format, getQuarter, parseISO, startOfMonth, startOfQuarter, startOfYear } from 'date-fns'

/** A report period (kỳ): a calendar month, quarter or year. */
export type PeriodUnit = 'month' | 'quarter' | 'year'

/** What a list is filtered by in time: nothing, one period, or two dates the user typed. */
export type PeriodKind = 'all' | PeriodUnit | 'custom'

/** First and last day, yyyy-MM-dd, both included. */
export interface DateRange {
  from: string
  to: string
}

const DAY = 'yyyy-MM-dd'
const day = (d: Date) => format(d, DAY)

/** The days of the period that contains `anchor`. */
export function periodRange(unit: PeriodUnit, anchor: Date): DateRange {
  if (unit === 'month') return { from: day(startOfMonth(anchor)), to: day(endOfMonth(anchor)) }
  if (unit === 'quarter') return { from: day(startOfQuarter(anchor)), to: day(endOfQuarter(anchor)) }
  return { from: day(startOfYear(anchor)), to: day(endOfYear(anchor)) }
}

/** A date inside the previous (-1) or next (+1) period. */
export function shiftPeriod(unit: PeriodUnit, anchor: Date, by: number): Date {
  if (unit === 'month') return addMonths(anchor, by)
  if (unit === 'quarter') return addQuarters(anchor, by)
  return addYears(anchor, by)
}

/** "2026-11", "Q4 2026" or "2026". */
export function periodLabel(unit: PeriodUnit, anchor: Date): string {
  if (unit === 'month') return format(anchor, 'yyyy-MM')
  if (unit === 'quarter') return `Q${getQuarter(anchor)} ${format(anchor, 'yyyy')}`
  return format(anchor, 'yyyy')
}

/** The twelve (month) / four (quarter) / one (year) months of a period as { year, month } pairs, for services that filter by month only. */
export function periodMonths(unit: PeriodUnit, anchor: Date): { year: number; month: number }[] {
  const { from, to } = periodRange(unit, anchor)
  const out: { year: number; month: number }[] = []
  for (let d = startOfMonth(parseISO(from)); d <= parseISO(to); d = addMonths(d, 1)) out.push({ year: d.getFullYear(), month: d.getMonth() + 1 })
  return out
}

/** True for a yyyy-MM-dd text that is a real calendar day. */
export function isDay(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const d = parseISO(value)
  return !Number.isNaN(d.getTime()) && day(d) === value
}

export const todayText = (now = new Date()) => day(now)
