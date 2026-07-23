import { useMemo, useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { suppliersApi } from '@/api/suppliers.api'
import { DataTable } from '@/components/shared/DataTable'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Pagination } from '@/components/shared/Pagination'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { usePagination } from '@/hooks/usePagination'
import { usePermission } from '@/hooks/usePermission'
import type { Supplier } from '@/api/suppliers.api'

type ActiveFilter = '' | 'active' | 'inactive'

const STATUS_FILTERS: { labelKey: string; value: ActiveFilter }[] = [
  { labelKey: 'common.all', value: '' },
  { labelKey: 'purchasing.suppliers.filters.active', value: 'active' },
  { labelKey: 'purchasing.suppliers.filters.inactive', value: 'inactive' },
]

export function SupplierListPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const { page, pageSize, setPage } = usePagination()
  const [activeFilter, setActiveFilter] = useState<ActiveFilter>('')
  const [deactivateId, setDeactivateId] = useState<string | null>(null)
  const queryClient = useQueryClient()

  const isActive = activeFilter === '' ? undefined : activeFilter === 'active'

  // SuppliersController's GET / currently returns a plain array (not the
  // { items, totalCount, ... } paginated shape used elsewhere in this app) and
  // its handler ignores pageNumber/pageSize today — see suppliers.api.ts's
  // `list` doc comment. Pagination is therefore done client-side below.
  const { data, isLoading } = useQuery({
    queryKey: ['suppliers', { isActive }],
    queryFn: () => suppliersApi.list({ isActive }),
    staleTime: 30_000,
  })

  const allSuppliers = data ?? []
  const totalCount = allSuppliers.length
  const pagedSuppliers = useMemo(
    () => allSuppliers.slice((page - 1) * pageSize, page * pageSize),
    [allSuppliers, page, pageSize],
  )

  const deactivateMutation = useMutation({
    mutationFn: (id: string) => suppliersApi.deactivate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
      setDeactivateId(null)
    },
  })

  const handleFilterChange = (value: ActiveFilter) => {
    setActiveFilter(value)
    setPage(1)
  }

  const handleDeactivateConfirm = () => {
    if (deactivateId) deactivateMutation.mutate(deactivateId)
  }

  const columns = useMemo(
    () => [
      {
        key: 'code',
        label: t('purchasing.suppliers.code'),
        render: (s: Supplier) => (
          <Link to={`/purchasing/suppliers/${s.id}`} className="text-primary-600 hover:underline">
            {s.code}
          </Link>
        ),
      },
      { key: 'name', label: t('purchasing.suppliers.name') },
      {
        key: 'contactPerson',
        label: t('purchasing.suppliers.contactPerson'),
        render: (s: Supplier) => s.contactPerson ?? '—',
      },
      {
        key: 'email',
        label: t('purchasing.suppliers.email'),
        render: (s: Supplier) => s.email ?? '—',
      },
      {
        key: 'phone',
        label: t('purchasing.suppliers.phone'),
        render: (s: Supplier) => s.phone ?? '—',
      },
      { key: 'currencyCode', label: t('purchasing.suppliers.currencyCode') },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (s: Supplier) => (
          <StatusBadge
            status={s.isActive ? 'Active' : 'Inactive'}
            label={s.isActive ? t('common.active') : t('common.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (s: Supplier) => (
          <div className="flex gap-2">
            {hasPermission('supplier:update') && (
              <Link to={`/purchasing/suppliers/${s.id}`} className="text-xs text-primary-600 hover:underline">
                {t('common.edit')}
              </Link>
            )}
            {hasPermission('supplier:deactivate') && s.isActive && (
              <button
                onClick={() => setDeactivateId(s.id)}
                className="text-xs text-red-600 hover:underline"
              >
                {t('purchasing.suppliers.deactivate')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [hasPermission, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('purchasing.suppliers.title')}</h1>
        {hasPermission('supplier:create') && (
          <Link to="/purchasing/suppliers/new" className="btn-primary">
            {t('purchasing.suppliers.add')}
          </Link>
        )}
      </div>

      <div className="flex gap-1">
        {STATUS_FILTERS.map((f) => (
          <button
            key={f.value}
            onClick={() => handleFilterChange(f.value)}
            className={`rounded px-3 py-1.5 text-sm ${
              activeFilter === f.value
                ? 'bg-primary-600 text-white'
                : 'bg-white border border-gray-300 text-gray-600 hover:bg-gray-50'
            }`}
          >
            {t(f.labelKey)}
          </button>
        ))}
      </div>

      <DataTable columns={columns} data={pagedSuppliers} isLoading={isLoading} />
      <Pagination page={page} pageSize={pageSize} totalCount={totalCount} onPageChange={setPage} />

      <ConfirmDialog
        open={deactivateId !== null}
        title={t('purchasing.suppliers.deactivateTitle')}
        message={t('purchasing.suppliers.deactivateMessage')}
        confirmLabel={t('purchasing.suppliers.deactivate')}
        danger
        onConfirm={handleDeactivateConfirm}
        onCancel={() => setDeactivateId(null)}
      />
    </div>
  )
}
