import { cx } from './cx'

/** A grey placeholder block with a 1.2 s shimmer (static under reduced motion). Size it with className. */
export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      aria-hidden="true"
      className={cx('rounded bg-surface-muted motion-safe:animate-[pulse_1.2s_cubic-bezier(0.4,0,0.6,1)_infinite]', className ?? 'h-4 w-full')}
    />
  )
}

/** Table-shaped skeleton: `rows` x `columns` cells inside a table body. */
export function SkeletonRows({ rows = 5, columns }: { rows?: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, r) => (
        <tr key={r}>
          {Array.from({ length: columns }, (_, c) => (
            <td key={c} className="px-4 py-3">
              <Skeleton className={c === 0 ? 'h-4 w-3/4' : 'h-4 w-1/2'} />
            </td>
          ))}
        </tr>
      ))}
    </>
  )
}
