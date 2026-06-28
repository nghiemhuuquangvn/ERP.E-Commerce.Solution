import { useState, useMemo, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { productsApi } from '@/api/products.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import type { Product } from '@/api/products.api'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function ProductListPage() {
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['products', { page, pageSize, search }],
    queryFn: () => productsApi.list({ page, pageSize, search: search || undefined }),
    staleTime: 30_000,
  })

  const handleSearchChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setSearch(e.target.value)
      setPage(1)
    },
    [setPage],
  )

  const columns = useMemo(
    () => [
      { key: 'sku', label: 'SKU', className: 'font-mono text-xs' },
      { key: 'name', label: 'Name' },
      { key: 'categoryName', label: 'Category' },
      {
        key: 'price',
        label: 'Price',
        render: (p: Product) => fmt.format(p.price),
        className: 'text-right',
      },
      {
        key: 'stockQuantity',
        label: 'Stock',
        render: (p: Product) => (
          <span
            className={
              p.stockQuantity === 0
                ? 'text-red-600'
                : p.stockQuantity < 10
                  ? 'text-yellow-600'
                  : ''
            }
          >
            {p.stockQuantity}
          </span>
        ),
        className: 'text-right',
      },
      {
        key: 'status',
        label: 'Status',
        render: (p: Product) => <StatusBadge status={p.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: Product) =>
          hasPermission('products:edit') ? (
            <a href={`/products/${p.id}`} className="text-xs text-primary-600 hover:underline">
              Edit
            </a>
          ) : null,
      },
    ],
    [hasPermission],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Products</h1>
        {hasPermission('products:create') && (
          <button className="btn-primary">+ Add Product</button>
        )}
      </div>

      <input
        className="input max-w-xs"
        placeholder="Search by name or SKU…"
        value={search}
        onChange={handleSearchChange}
      />

      <DataTable columns={columns} data={data?.items ?? []} isLoading={isLoading} />
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={data?.totalCount ?? 0}
        onPageChange={setPage}
      />
    </div>
  )
}
