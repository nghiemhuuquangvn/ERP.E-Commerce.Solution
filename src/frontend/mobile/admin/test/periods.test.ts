import { isDay, periodLabel, periodMonths, periodRange, shiftPeriod, todayText } from '@/lib/periods'

const d = (text: string) => new Date(`${text}T12:00:00`)

describe('period ranges (kỳ)', () => {
  it('a month runs from its first to its last day', () => {
    expect(periodRange('month', d('2026-11-15'))).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(periodRange('month', d('2026-12-31'))).toEqual({ from: '2026-12-01', to: '2026-12-31' })
  })

  it('February knows leap years', () => {
    expect(periodRange('month', d('2028-02-10')).to).toBe('2028-02-29')
    expect(periodRange('month', d('2026-02-10')).to).toBe('2026-02-28')
  })

  it('a quarter covers three months, from its first day to its last', () => {
    expect(periodRange('quarter', d('2026-11-15'))).toEqual({ from: '2026-10-01', to: '2026-12-31' })
    expect(periodRange('quarter', d('2026-01-01'))).toEqual({ from: '2026-01-01', to: '2026-03-31' })
    expect(periodRange('quarter', d('2026-06-30'))).toEqual({ from: '2026-04-01', to: '2026-06-30' })
  })

  it('a year is January to December', () => {
    expect(periodRange('year', d('2026-07-04'))).toEqual({ from: '2026-01-01', to: '2026-12-31' })
  })
})

describe('stepping and labels', () => {
  it('moves to the previous and the next period across a year end', () => {
    expect(periodLabel('month', shiftPeriod('month', d('2026-01-20'), -1))).toBe('2025-12')
    expect(periodLabel('month', shiftPeriod('month', d('2026-12-20'), 1))).toBe('2027-01')
    expect(periodLabel('quarter', shiftPeriod('quarter', d('2026-02-01'), -1))).toBe('Q4 2025')
    expect(periodLabel('year', shiftPeriod('year', d('2026-02-01'), 1))).toBe('2027')
  })

  it('labels a month, a quarter and a year', () => {
    expect(periodLabel('month', d('2026-11-15'))).toBe('2026-11')
    expect(periodLabel('quarter', d('2026-11-15'))).toBe('Q4 2026')
    expect(periodLabel('year', d('2026-11-15'))).toBe('2026')
  })

  it('lists the months of a period', () => {
    expect(periodMonths('quarter', d('2026-11-15'))).toEqual([{ year: 2026, month: 10 }, { year: 2026, month: 11 }, { year: 2026, month: 12 }])
    expect(periodMonths('month', d('2026-02-03'))).toEqual([{ year: 2026, month: 2 }])
    expect(periodMonths('year', d('2026-02-03'))).toHaveLength(12)
  })
})

describe('day text', () => {
  it('accepts only real calendar days', () => {
    expect(isDay('2026-11-30')).toBe(true)
    expect(isDay('2028-02-29')).toBe(true)
    expect(isDay('2026-02-29')).toBe(false)
    expect(isDay('2026-13-01')).toBe(false)
    expect(isDay('2026-1-1')).toBe(false)
    expect(isDay('')).toBe(false)
    expect(isDay('tomorrow')).toBe(false)
  })

  it('today as text', () => {
    expect(todayText(d('2026-11-04'))).toBe('2026-11-04')
  })
})
