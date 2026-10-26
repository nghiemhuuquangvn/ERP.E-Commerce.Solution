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
        <Link to={`/inventory/${i.productId}`} className="text-primary-600 hover:underline">
          {i.productName}
        </Link>
      ),
    },
    {
      key: 'totalQuantity',
      label: t('inventory.onHand'),
      className: 'text-right',
      render: (i: InventoryListItem) => (
        <span className={i.totalQuantity === 0 ? 'font-medium text-red-600' : ''}>{i.totalQuantity}</span>
      ),
    },
    {
      key: 'availableQuantity',
      label: t('inventory.available'),
      className: 'text-right',
      render: (i: InventoryListItem) => (
        <span className={i.availableQuantity < 5 ? 'font-medium text-yellow-600' : 'font-medium text-green-600'}>
          {i.availableQuantity}
        </span>
      ),
    },
    { key: 'minStock', label: t('inventory.policy.minStock'), className: 'text-right' },
    { key: 'maxStock', label: t('inventory.policy.maxStock'), className: 'text-right' },
    {
      key: 'status',
      label: t('common.status'),
      render: (i: InventoryListItem) => <StatusBadge status={i.status === 'OK' ? 'Active' : 'Pending'} label={i.status} />,
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('inventory.title')}</h1>
      </div>

      <div className="flex gap-3">
        <input
          className="input max-w-xs"
          placeholder={t('inventory.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <label className="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
          <input
            type="checkbox"
            checked={lowStockOnly}
            onChange={(e) => { setLowStockOnly(e.target.checked); setPage(1) }}
            className="rounded border-gray-300"
          />
          {t('inventory.lowStockOnly')}
        </label>
      </div>

      <DataTable
        columns={columns}
        data={(data?.items ?? []).map((i) => ({ ...i, id: i.productId }))}
        isLoading={isLoading}
      />
      <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
    </div>
  )
}
