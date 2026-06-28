import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { inventoryApi } from '@/api/inventory.api'
import { DataTable } from '@/components/shared/DataTable'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { format } from 'date-fns'
import type { InventoryItem } from '@/api/inventory.api'

export function InventoryListPage() {
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [lowStockOnly, setLowStockOnly] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['inventory', { page, pageSize, search, lowStockOnly }],
    queryFn: () =>
      inventoryApi.list({ page, pageSize, search: search || undefined, lowStock: lowStockOnly || undefined }),
  })

  const columns = [
    { key: 'sku', label: 'SKU', className: 'font-mono text-xs' },
    { key: 'productName', label: 'Product' },
    { key: 'warehouseLocation', label: 'Location' },
    {
      key: 'quantityOnHand',
      label: 'On Hand',
      render: (i: InventoryItem) => (
        <span className={i.quantityOnHand === 0 ? 'font-medium text-red-600' : ''}>{i.quantityOnHand}</span>
      ),
      className: 'text-right',
    },
    { key: 'quantityReserved', label: 'Reserved', className: 'text-right' },
    {
      key: 'quantityAvailable',
      label: 'Available',
      render: (i: InventoryItem) => (
        <span className={i.quantityAvailable < 5 ? 'font-medium text-yellow-600' : 'font-medium text-green-600'}>
          {i.quantityAvailable}
        </span>
      ),
      className: 'text-right',
    },
    {
      key: 'lastMovementAt',
      label: 'Last Movement',
      render: (i: InventoryItem) => format(new Date(i.lastMovementAt), 'dd MMM yyyy'),
    },
  ]

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Inventory</h1>
      </div>

      <div className="flex gap-3">
        <input
          className="input max-w-xs"
          placeholder="Search by product or SKU…"
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
          Low stock only
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
