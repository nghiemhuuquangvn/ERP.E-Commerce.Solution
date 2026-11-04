import { useCallback, useEffect, useMemo, useState } from 'react'
import { isDay, periodLabel, periodRange, shiftPeriod, type PeriodKind, type PeriodUnit } from '@/lib/periods'

/** The search text is sent to the server this long after the last key. */
export const SEARCH_DEBOUNCE_MS = 400

/** What a list sends to the server (empty values are left out). */
export interface ListParams {
  status?: string
  dateFrom?: string
  dateTo?: string
  search?: string
}

interface Options {
  /** Status the list opens on ('' or undefined = all). */
  status?: string
  /** Time filter the list opens on. */
  kind?: PeriodKind
  /** Today, for tests. */
  now?: Date
}

/**
 * The filters of one list: status, a period (month / quarter / year, stepped with previous / next) or a custom from-to range,
 * and the text typed in the search box (debounced). `params` is what goes into the request and into the react-query key.
 */
export function useListFilters({ status: initialStatus = '', kind: initialKind = 'all', now }: Options = {}) {
  const today = useMemo(() => now ?? new Date(), [now])
  const [status, setStatus] = useState(initialStatus)
  const [kind, setKindState] = useState<PeriodKind>(initialKind)
  const [anchor, setAnchor] = useState<Date>(today)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [search, setSearch] = useState('')
  const [debounced, setDebounced] = useState('')

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(search.trim()), SEARCH_DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [search])

  const range = useMemo<{ from?: string; to?: string }>(() => {
    if (kind === 'all') return {}
    if (kind === 'custom') return { from: isDay(from) ? from : undefined, to: isDay(to) ? to : undefined }
    return periodRange(kind, anchor)
  }, [kind, anchor, from, to])

  /** A custom range that ends before it starts is not sent; the sheet shows why. */
  const rangeError = kind === 'custom' && !!range.from && !!range.to && range.from > range.to

  const params = useMemo<ListParams>(() => {
    const p: ListParams = {}
    if (status) p.status = status
    if (!rangeError) {
      if (range.from) p.dateFrom = range.from
      if (range.to) p.dateTo = range.to
    }
    if (debounced) p.search = debounced
    return p
  }, [status, range, rangeError, debounced])

  const setKind = useCallback((next: PeriodKind) => {
    setKindState(next)
    if (next !== 'custom' && next !== 'all') setAnchor(today)
  }, [today])

  const step = useCallback((by: number) => {
    if (kind === 'all' || kind === 'custom') return
    setAnchor((a) => shiftPeriod(kind, a, by))
  }, [kind])

  const reset = useCallback(() => {
    setStatus(initialStatus)
    setKindState(initialKind)
    setAnchor(today)
    setFrom('')
    setTo('')
    setSearch('')
    setDebounced('')
  }, [initialStatus, initialKind, today])

  /** Short text for the summary line: "2026-11", "Q4 2026", "2026", "2026-11-01 → 2026-11-30". */
  const periodText = useMemo(() => {
    if (kind === 'all') return undefined
    if (kind === 'custom') return range.from || range.to ? `${range.from ?? '…'} → ${range.to ?? '…'}` : undefined
    return periodLabel(kind as PeriodUnit, anchor)
  }, [kind, range, anchor])

  // The search box is not counted: it is always visible on the list; this counts what the Filters sheet holds.
  const activeCount = (status ? 1 : 0) + (periodText ? 1 : 0)

  return { status, kind, anchor, from, to, search, params, periodText, rangeError, activeCount, setStatus, setKind, step, setFrom, setTo, setSearch, reset }
}

export type ListFilters = ReturnType<typeof useListFilters>
