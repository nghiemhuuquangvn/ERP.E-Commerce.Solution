import React from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react'
import { EmptyState } from '@/components/ui/EmptyState'
import { SkeletonRows } from '@/components/ui/Skeleton'
import { cx } from '@/components/ui/cx'

interface Column<T> {
  key: keyof T | string
  label: string
  render?: (row: T) => React.ReactNode
  className?: string
  /** Numbers and money read better right-aligned. */
  align?: 'left' | 'right'
  /** Shows a sort button in the header; needs `sortKey`, `sortDir` and `onSort` on the table. */
  sortable?: boolean
}

interface DataTableProps<T extends { id: string }> {
  columns: Column<T>[]
  data: T[]
  /** Shows skeleton rows in the table's own shape while the list loads. */
  isLoading?: boolean
  /** The text of the default empty state (default: "No records found"). */
  emptyMessage?: string
  /** Replaces the default empty state, e.g. a "no results" state with a clear-filters button. */
  emptyState?: React.ReactNode
  /** Keeps the header visible while the body scrolls (the table then scrolls inside a 70 vh box). */
  stickyHeader?: boolean
  sortKey?: string
  sortDir?: 'asc' | 'desc'
  onSort?: (key: string) => void
}

function DataTableInner<T extends { id: string }>({
  columns,
  data,
  isLoading,
  emptyMessage,
  emptyState,
  stickyHeader,
  sortKey,
  sortDir,
  onSort,
}: DataTableProps<T>) {
  const { t } = useTranslation()

  return (
    <div className={cx('overflow-x-auto rounded-lg border border-line', stickyHeader && 'max-h-[70vh] overflow-y-auto')} aria-busy={isLoading || undefined}>
      <table className="min-w-full divide-y divide-line bg-surface text-sm">
        <thead className={cx('bg-surface-muted', stickyHeader && 'sticky top-0 z-10')}>
          <tr>
            {columns.map((col) => {
              const key = String(col.key)
              const active = col.sortable && sortKey === key
              const align = col.align === 'right' ? 'text-right' : 'text-left'
              return (
                <th
                  key={key}
                  scope="col"
                  aria-sort={active ? (sortDir === 'desc' ? 'descending' : 'ascending') : col.sortable ? 'none' : undefined}
                  className={cx('px-4 py-3 text-xs font-semibold uppercase tracking-wider text-ink-secondary', align, col.className)}
                >
                  {col.sortable && onSort ? (
                    <button
                      type="button"
                      onClick={() => onSort(key)}
                      className={cx('inline-flex items-center gap-1 uppercase tracking-wider hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action', col.align === 'right' && 'flex-row-reverse')}
                    >
                      {col.label}
                      {active ? (sortDir === 'desc' ? <ArrowDown size={14} aria-hidden="true" /> : <ArrowUp size={14} aria-hidden="true" />) : <ArrowUpDown size={14} aria-hidden="true" className="opacity-50" />}
                    </button>
                  ) : (
                    col.label
                  )}
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {isLoading ? (
            <SkeletonRows columns={columns.length} />
          ) : data.length === 0 ? (
            <tr>
              <td colSpan={columns.length}>
                {emptyState ?? <EmptyState title={emptyMessage ?? t('common.noRecordsFound')} />}
              </td>
            </tr>
          ) : (
            data.map((row) => (
              <tr key={row.id} className="hover:bg-surface-muted">
                {columns.map((col) => (
                  <td key={String(col.key)} className={cx('px-4 py-3', col.align === 'right' && 'text-right tabular-nums', col.className)}>
                    {col.render
                      ? col.render(row)
                      : String((row as Record<string, unknown>)[String(col.key)] ?? '')}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}

// React.memo cast preserves the generic type parameter <T>
export const DataTable = React.memo(DataTableInner) as typeof DataTableInner
