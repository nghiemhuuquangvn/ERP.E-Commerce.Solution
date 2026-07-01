import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { inventoryApi } from '@/api/inventory.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { format } from 'date-fns'
import type { InventoryItem } from '@/api/inventory.api'

export function InventoryListPage() {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['inventory', { page, pageSize, search, lowStockOnly }],
    queryFn: () =>
      inventoryApi.list({ page, pageSize, search: search || undefined, lowStock: lowStockOnly || undefined }),
  })

  const columns = [
    { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
    { key: 'productName', label: t('orders.product') },
    { key: 'warehouseLocation', label: t('inventory.location') },
    {
      key: 'quantityOnHand',
      label: t('inventory.onHand'),
      render: (i: InventoryItem) => (
        <span className={i.quantityOnHand === 0 ? 'font-medium text-red-600' : ''}>{i.quantityOnHand}</span>
      ),
      className: 'text-right',
    },
    { key: 'quantityReserved', label: t('inventory.reserved'), className: 'text-right' },
    {
      key: 'quantityAvailable',
      label: t('inventory.available'),
      render: (i: InventoryItem) => (
        <span className={i.quantityAvailable < 5 ? 'font-medium text-yellow-600' : 'font-medium text-green-600'}>
          {i.quantityAvailable}
        </span>
      ),
      className: 'text-right',
    },
    {
      key: 'lastMovementAt',
      label: t('inventory.lastMovement'),
      render: (i: InventoryItem) => format(new Date(i.lastMovementAt), 'dd MMM yyyy'),
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
