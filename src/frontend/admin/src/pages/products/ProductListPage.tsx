import { useState, useMemo, useCallback } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { productsApi, productsImportEndpoints, PRODUCT_STATUS, PRODUCT_STATUS_LABELS } from '@/api/products.api'
import { ImportDialog } from '@/components/import/ImportDialog'
import { FileUp } from 'lucide-react'
import { brandsApi } from '@/api/product/brands.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import type { Product } from '@/api/products.api'
import { Button, EmptyState, Input } from '@/components/ui'

const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

const STATUS_FILTERS: { labelKey: string; value: number | '' }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'products.statusFilters.draft', value: PRODUCT_STATUS.Draft },
  { labelKey: 'products.statusFilters.pendingReview', value: PRODUCT_STATUS.PendingReview },
  { labelKey: 'products.statusFilters.active', value: PRODUCT_STATUS.Active },
  { labelKey: 'products.statusFilters.discontinued', value: PRODUCT_STATUS.Discontinued },
]

export function ProductListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<number | ''>('')
  const [importOpen, setImportOpen] = useState(false)
  const queryClient = useQueryClient()

  const searchTerm = search.trim()

  // ProductsController.ListProducts has no `search` query param (only Skip/Take/
  // CategoryId/Status) — free-text search must go through the dedicated `/search`
  // endpoint instead, which returns a flat, unpaginated array (capped at 50).
  const { data, isLoading } = useQuery({
    queryKey: ['products', searchTerm ? 'search' : 'list', { searchTerm, page, pageSize, status }],
    queryFn: async () => {
      if (searchTerm) {
        const items = await productsApi.search(searchTerm)
        return { products: items, total: items.length }
      }
      const skip = (page - 1) * pageSize
      const res = await productsApi.list({ skip, take: pageSize, status: status === '' ? undefined : status })
      return { products: res.products, total: res.total }
    },
    staleTime: 30_000,
  })

  // Brands are a small flat reference list (no pagination on the backend), so a
  // single bulk fetch + client-side id->name lookup is the "reasonably easy" join
  // for the list's Brand column, same pattern DivisionsPage uses for Branch names.
  const { data: brands } = useQuery({
    queryKey: ['product', 'brands', 'picker'],
    queryFn: () => brandsApi.list(),
    staleTime: 60_000,
  })

  const brandName = useCallback(
    (brandId?: string | null) => {
      if (!brandId) return '—'
      return brands?.find((b) => b.id === brandId)?.name ?? brandId
    },
    [brands],
  )

  const handleSearchChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setSearch(e.target.value)
      setPage(1)
    },
    [setPage],
  )

  const handleStatusChange = useCallback(
    (value: number | '') => {
      setStatus(value)
      setPage(1)
    },
    [setPage],
  )

  const columns = useMemo(
    () => [
      { key: 'sku', label: t('products.sku'), className: 'font-mono text-xs' },
      {
        key: 'name',
        label: t('common.name'),
        render: (p: Product) => (
          <Link to={`/products/${p.id}`} className="text-brand-action hover:underline">
            {p.name}
          </Link>
        ),
      },
      { key: 'categoryName', label: t('products.category') },
      {
        key: 'brandId',
        label: t('products.brand'),
        render: (p: Product) => brandName(p.brandId),
      },
      {
        key: 'price',
        label: t('products.price'),
        render: (p: Product) => fmt.format(p.price),
        align: 'right' as const,
      },
      {
        key: 'stockQuantity',
        label: t('products.stock'),
        render: (p: Product) => (
          <span
            className={
              p.stockQuantity === 0
                ? 'text-danger-soft-text'
                : p.stockQuantity < 10
                  ? 'text-warning-soft-text'
                  : ''
            }
          >
            {p.stockQuantity}
          </span>
        ),
        align: 'right' as const,
      },
      {
        key: 'status',
        label: t('common.status'),
        render: (p: Product) => <StatusBadge status={PRODUCT_STATUS_LABELS[p.status] ?? String(p.status)} />,
      },
      {
        key: 'actions',
        label: '',
        render: (p: Product) =>
          hasPermission('products:edit') ? (
            <Link to={`/products/${p.id}`} className="text-xs text-brand-action hover:underline">
              {t('common.edit')}
            </Link>
          ) : null,
      },
    ],
    [hasPermission, brandName, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.title')}</h1>
        <div className="flex items-center gap-2">
          {hasPermission('products:manage') && (
            <Button variant="outline" onClick={() => setImportOpen(true)}>
              <FileUp size={16} strokeWidth={1.75} aria-hidden="true" />
              {t('import.products.title')}
            </Button>
          )}
          {hasPermission('products:create') && (
            <Link to="/products/new" className="btn-primary">
              {t('products.addProduct')}
            </Link>
          )}
        </div>
      </div>

      <ImportDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
        titleKey="import.products.title"
        columnsKey="import.products.columns"
        endpoints={productsImportEndpoints}
        templateFileName="products-import-template.csv"
        onFinished={() => void queryClient.invalidateQueries({ queryKey: ['products'] })}
      />

      <div className="flex flex-wrap items-center gap-3">
        <Input
          className="max-w-xs"
        aria-label={t('products.searchPlaceholder')}
          placeholder={t('products.searchPlaceholder')}
          value={search}
          onChange={handleSearchChange}
        />
        <div className="flex gap-1">
          {STATUS_FILTERS.map((f) => (
            <Button
              key={f.labelKey}
              size="sm"
              variant={status === f.value ? 'primary' : 'outline'}
              aria-pressed={status === f.value}
              onClick={() => handleStatusChange(f.value)}
            >
              {t(f.labelKey)}
            </Button>
          ))}
        </div>
      </div>

      <DataTable
        columns={columns}
        data={data?.products ?? []}
        isLoading={isLoading}
        emptyState={
          searchTerm || status !== '' ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => { setSearch(''); handleStatusChange('') }}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
      />
      {!searchTerm && (
        <Pagination page={page} pageSize={pageSize} totalCount={data?.total ?? 0} onPageChange={setPage} />
      )}
    </div>
  )
}
