import { useInfiniteQuery } from '@tanstack/react-query'

export interface Page<T> {
  items: T[]
  total: number
}

export const PAGE_SIZE = 20

/** A list the server answers whole (no paging): one page holding every row. */
export const onePage = <T>(items: T[]): Page<T> => ({ items, total: items.length })

/**
 * A page of a list that answers a bare array without a total: a full page means "there may be another one" (the total is reported as one
 * more than what was loaded), a short page is the end.
 */
export function pageOfArray<T>(rows: T[], page: number, pageSize = PAGE_SIZE): Page<T> {
  const before = (page - 1) * pageSize
  return { items: rows, total: rows.length < pageSize ? before + rows.length : before + rows.length + 1 }
}

/**
 * An endless list over a paged endpoint: page 1 first, the next page when the list end is reached, pull-to-refresh refetches
 * from page 1. `fetchPage` returns the rows of one page and the total (each module normalises its own server shape).
 */
export function usePagedList<T>(key: readonly unknown[], fetchPage: (page: number) => Promise<Page<T>>) {
  const q = useInfiniteQuery({
    queryKey: key,
    initialPageParam: 1,
    queryFn: ({ pageParam }) => fetchPage(pageParam),
    getNextPageParam: (last, all) => (all.reduce((n, p) => n + p.items.length, 0) < last.total && last.items.length > 0 ? all.length + 1 : undefined),
  })
  return { ...q, items: q.data?.pages.flatMap((p) => p.items) ?? [] }
}
