import { useState, useCallback } from 'react'

export function usePagination(defaultPageSize = 20) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(defaultPageSize)

  const reset = useCallback(() => setPage(1), [])

  return { page, pageSize, setPage, setPageSize, reset }
}
