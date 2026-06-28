import React, { useMemo } from 'react'

interface PaginationProps {
  page: number
  pageSize: number
  totalCount: number
  onPageChange: (page: number) => void
}

function PaginationInner({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const totalPages = Math.ceil(totalCount / pageSize)
  if (totalPages <= 1) return null

  const pages = useMemo(() => {
    const start = Math.max(1, page - 2)
    return Array.from({ length: Math.min(5, totalPages) }, (_, i) => start + i).filter(
      (p) => p <= totalPages,
    )
  }, [page, totalPages])

  return (
    <div className="flex items-center justify-between py-3 text-sm text-gray-600">
      <span>
        Showing {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} of{' '}
        {totalCount}
      </span>
      <div className="flex gap-1">
        <button
          disabled={page === 1}
          onClick={() => onPageChange(page - 1)}
          className="rounded px-3 py-1 hover:bg-gray-100 disabled:opacity-40"
        >
          ‹ Prev
        </button>
        {pages.map((p) => (
          <button
            key={p}
            onClick={() => onPageChange(p)}
            className={`rounded px-3 py-1 ${
              p === page ? 'bg-primary-600 text-white' : 'hover:bg-gray-100'
            }`}
          >
            {p}
          </button>
        ))}
        <button
          disabled={page === totalPages}
          onClick={() => onPageChange(page + 1)}
          className="rounded px-3 py-1 hover:bg-gray-100 disabled:opacity-40"
        >
          Next ›
        </button>
      </div>
    </div>
  )
}

export const Pagination = React.memo(PaginationInner)
