import { useState, useMemo, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import type { Product } from '@/api/products.api'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function ProductListPage() {
  const { t } = useTranslation()
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
      { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'categoryName', label: t('products.category') },
      {
        key: 'price',
        label: t('products.price'),
        render: (p: Product) => fmt.format(p.price),
        className: 'text-right',
      },
      {
        key: 'stockQuantity',
        label: t('products.stock'),
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
        label: t('common.status'),
        render: (p: Product) => <StatusBadge status={p.status} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: Product) =>
          hasPermission('products:edit') ? (
            <a href={`/products/${p.id}`} className="text-xs text-primary-600 hover:underline">
              {t('common.edit')}
            </a>
          ) : null,
      },
    ],
    [hasPermission, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.title')}</h1>
        {hasPermission('products:create') && (
          <button className="btn-primary">{t('products.addProduct')}</button>
        )}
      </div>

      <input
        className="input max-w-xs"
        placeholder={t('products.searchPlaceholder')}
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
