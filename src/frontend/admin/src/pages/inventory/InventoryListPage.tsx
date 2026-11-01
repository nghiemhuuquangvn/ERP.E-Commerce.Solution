import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { inventoryApi } from '@/api/inventory.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import type { InventoryListItem } from '@/api/inventory.api'
import { Button, Checkbox, EmptyState, Input } from '@/components/ui'

export function InventoryListPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['inventory', { page, pageSize, search, lowStockOnly }],
    queryFn: () =>
      inventoryApi.list({ page, pageSize, search: search || undefined, lowStockOnly: lowStockOnly || undefined }),
  })

  const columns = [
    {
      key: 'productName',
      label: t('orders.product'),
      render: (i: InventoryListItem) => (
        <Link to={`/inventory/${i.productId}`} className="text-brand-action hover:underline">
          {i.productName}
        </Link>
      ),
    },
    {
      key: 'totalQuantity',
      label: t('inventory.onHand'),
      align: 'right' as const,
      render: (i: InventoryListItem) => (
        <span className={`tabular-nums ${i.totalQuantity === 0 ? 'font-medium text-danger-soft-text' : ''}`}>{i.totalQuantity}</span>
      ),
    },
    {
      key: 'availableQuantity',
      label: t('inventory.available'),
      align: 'right' as const,
      render: (i: InventoryListItem) => (
        <span className={`font-medium tabular-nums ${i.availableQuantity < 5 ? 'text-warning-soft-text' : 'text-success-soft-text'}`}>
          {i.availableQuantity}
        </span>
      ),
    },
    { key: 'minStock', label: t('inventory.policy.minStock'), align: 'right' as const },
    { key: 'maxStock', label: t('inventory.policy.maxStock'), align: 'right' as const },
    {
      key: 'status',
      label: t('common.status'),
      render: (i: InventoryListItem) => <StatusBadge status={i.status === 'OK' ? 'Active' : 'Pending'} label={i.status} />,
    },
  ]

  const filtered = Boolean(search) || lowStockOnly
  const clear = () => { setSearch(''); setLowStockOnly(false); setPage(1) }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('inventory.title')}</h1>
      </div>

      <div className="flex items-center gap-3">
        <Input
          className="max-w-xs"
          aria-label={t('inventory.searchPlaceholder')}
          placeholder={t('inventory.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <Checkbox
          label={t('inventory.lowStockOnly')}
          checked={lowStockOnly}
          onChange={(e) => { setLowStockOnly(e.target.checked); setPage(1) }}
        />
      </div>

      <DataTable
        columns={columns}
        data={(data?.items ?? []).map((i) => ({ ...i, id: i.productId }))}
        isLoading={isLoading}
        emptyState={
          filtered ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={clear}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
    </div>
  )
}
