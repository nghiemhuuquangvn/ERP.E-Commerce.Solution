import { useState, useMemo, useEffect, useCallback } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { productsApi } from '@/api/products.api'
import type { Product } from '@/api/products.api'
import { DataTable } from './DataTable'
import { Pagination } from './Pagination'
import { usePagination } from '@/hooks/usePagination'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

interface ProductPickerModalProps {
  open: boolean
  multiple?: boolean
  onConfirm: (products: Product[]) => void
  onClose: () => void
}

export function ProductPickerModal({ open, multiple = true, onConfirm, onClose }: ProductPickerModalProps) {
  const { t } = useTranslation()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Map<string, Product>>(new Map())

  // Reopening should start from a clean slate — no stale search text or
  // selection carried over from a previous "Add products" session.
  useEffect(() => {
    if (open) {
      setSearch('')
      setPage(1)
      setSelected(new Map())
    }
  }, [open, setPage])

  const searchTerm = search.trim()

  const { data, isLoading } = useQuery({
    queryKey: ['products', 'picker', searchTerm ? 'search' : 'list', { searchTerm, page, pageSize }],
    queryFn: async () => {
      if (searchTerm) {
        const items = await productsApi.search(searchTerm)
        return { products: items, total: items.length }
      }
      const skip = (page - 1) * pageSize
      const res = await productsApi.list({ skip, take: pageSize })
      return { products: res.products, total: res.total }
    },
    enabled: open,
    staleTime: 30_000,
  })

  const toggle = useCallback(
    (product: Product) => {
      setSelected((prev) => {
        const next = multiple ? new Map(prev) : new Map<string, Product>()
        if (prev.has(product.id)) {
          next.delete(product.id)
        } else {
          next.set(product.id, product)
        }
        return next
      })
    },
    [multiple],
  )

  const columns = useMemo(
    () => [
      {
        key: 'select',
        label: '',
        className: 'w-8',
        render: (p: Product) => (
          <input
            type={multiple ? 'checkbox' : 'radio'}
            checked={selected.has(p.id)}
            onChange={() => toggle(p)}
          />
        ),
      },
      {
        key: 'name',
        label: t('common.name'),
        render: (p: Product) => (
          <button type="button" onClick={() => toggle(p)} className="text-left hover:underline">
            {p.name}
          </button>
        ),
      },
      { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
      {
        key: 'price',
        label: t('products.price'),
        render: (p: Product) => fmt.format(p.price),
        className: 'text-right',
      },
      {
        key: 'stockQuantity',
        label: t('products.stock'),
        className: 'text-right',
      },
    ],
    [selected, multiple, toggle, t],
  )

  if (!open) return null

  const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value)
    setPage(1)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card flex max-h-[85vh] w-full max-w-2xl flex-col p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('productPicker.title')}</h3>

        <input
          className="input mb-3"
          placeholder={t('productPicker.searchPlaceholder')}
          value={search}
          onChange={handleSearchChange}
        />

        <div className="flex-1 overflow-y-auto">
          <DataTable columns={columns} data={data?.products ?? []} isLoading={isLoading} />
        </div>

        {!searchTerm && (
          <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
        )}

        <div className="mt-4 flex items-center justify-between border-t border-gray-100 pt-4">
          <span className="text-sm text-gray-500">
            {t('productPicker.selectedCount', { count: selected.size })}
          </span>
          <div className="flex gap-3">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button
              type="button"
              disabled={selected.size === 0}
              onClick={() => {
                onConfirm([...selected.values()])
                onClose()
              }}
              className="btn-primary"
            >
              {t('productPicker.confirmSelection')}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
