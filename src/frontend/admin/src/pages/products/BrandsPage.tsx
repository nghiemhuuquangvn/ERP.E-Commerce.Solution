import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { brandsApi } from '@/api/product/brands.api'
import type { Brand } from '@/api/product/brands.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface BrandFormValues {
  code: string
  name: string
  logoUrl: string
}

function BrandModal({ brand, onClose }: { brand: Brand | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!brand

  const { register, handleSubmit, formState: { errors } } = useForm<BrandFormValues>({
    defaultValues: {
      code: brand?.code ?? '',
      name: brand?.name ?? '',
      logoUrl: brand?.logoUrl ?? '',
    },
  })

  const mutation = useMutation({
    mutationFn: async (values: BrandFormValues) => {
      const logoUrl = values.logoUrl.trim() || undefined
      if (isEdit) {
        await brandsApi.update(brand!.id, { name: values.name, logoUrl })
      } else {
        await brandsApi.create({ code: values.code, name: values.name, logoUrl })
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'brands'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('products.brands.editTitle') : t('products.brands.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
            <input className="input" disabled={isEdit} {...register('code', { required: !isEdit })} />
            {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('products.brands.logoUrl')}</label>
            <input className="input" {...register('logoUrl')} />
          </div>
          {mutation.isError && (
            <p className="text-xs text-red-600">{t('products.brands.failedToSave')}</p>
          )}
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={onClose} className="btn-outline">
              {t('common.cancel')}
            </button>
            <button type="submit" disabled={mutation.isPending} className="btn-primary">
              {mutation.isPending ? t('common.saving') : t('common.save')}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function BrandsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all')
  const [modalState, setModalState] = useState<{ open: boolean; brand: Brand | null }>({
    open: false,
    brand: null,
  })
  const [confirmTarget, setConfirmTarget] = useState<Brand | null>(null)

  // GetBrandsQuery has no pagination — it always returns the full matching list.
  const isActiveParam = activeFilter === 'all' ? undefined : activeFilter === 'active'
  const { data, isLoading } = useQuery({
    queryKey: ['product', 'brands', { isActiveParam }],
    queryFn: () => brandsApi.list({ isActive: isActiveParam }),
    staleTime: 30_000,
  })

  const canManage = hasPermission('product:brands:manage')

  const toggleMutation = useMutation({
    mutationFn: (b: Brand) => (b.isActive ? brandsApi.deactivate(b.id) : brandsApi.activate(b.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['product', 'brands'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      {
        key: 'logoUrl',
        label: t('products.brands.logoUrl'),
        render: (b: Brand) => b.logoUrl ?? '—',
      },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (b: Brand) => (
          <StatusBadge
            status={b.isActive ? 'Active' : 'Inactive'}
            label={b.isActive ? t('products.brands.active') : t('products.brands.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (b: Brand) =>
          canManage ? (
            <div className="flex gap-3">
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, brand: b })}
              >
                {t('common.edit')}
              </button>
              {b.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(b)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button
                  className="text-xs text-green-600 hover:underline"
                  onClick={() => toggleMutation.mutate(b)}
                >
                  {t('common.activate')}
                </button>
              )}
            </div>
          ) : null,
      },
    ],
    [canManage, toggleMutation, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('products.brands.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, brand: null })}>
            {t('products.brands.add')}
          </button>
        )}
      </div>

      <select
        className="input max-w-xs"
        value={activeFilter}
        onChange={(e) => setActiveFilter(e.target.value as 'all' | 'active' | 'inactive')}
      >
        <option value="all">{t('common.all')}</option>
        <option value="active">{t('products.brands.active')}</option>
        <option value="inactive">{t('products.brands.inactive')}</option>
      </select>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalState.open && (
        <BrandModal brand={modalState.brand} onClose={() => setModalState({ open: false, brand: null })} />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('products.brands.deactivateTitle')}
        message={t('products.brands.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
