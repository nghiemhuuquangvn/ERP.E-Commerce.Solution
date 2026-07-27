import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { warehousesApi } from '@/api/warehouses.api'
import type { Warehouse } from '@/api/warehouses.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface WarehouseFormValues {
  code: string
  name: string
  address: string
}

function WarehouseModal({ warehouse, onClose }: { warehouse: Warehouse | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!warehouse

  const { register, handleSubmit, formState: { errors } } = useForm<WarehouseFormValues>({
    defaultValues: { code: warehouse?.code ?? '', name: warehouse?.name ?? '', address: warehouse?.address ?? '' },
  })

  const mutation = useMutation({
    mutationFn: (values: WarehouseFormValues) =>
      isEdit
        ? warehousesApi.update(warehouse!.id, { name: values.name, address: values.address || undefined })
        : warehousesApi.create({ code: values.code, name: values.name, address: values.address || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['warehouses'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('inventory.warehouses.editTitle') : t('inventory.warehouses.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('code', { required: true })} />
              {errors.code && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('name', { required: true })} />
            {errors.name && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.warehouses.address')}</label>
            <input className="input" {...register('address')} />
          </div>
          {mutation.isError && <p className="text-xs text-red-600">{t('common.failedToSave')}</p>}
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

export function WarehousesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; warehouse: Warehouse | null }>({ open: false, warehouse: null })
  const [confirmTarget, setConfirmTarget] = useState<Warehouse | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['warehouses'],
    queryFn: warehousesApi.list,
    staleTime: 30_000,
  })

  const canManage = hasPermission('inventory:warehouse:manage')

  const toggleMutation = useMutation({
    mutationFn: (w: Warehouse) => (w.isActive ? warehousesApi.deactivate(w.id) : warehousesApi.activate(w.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['warehouses'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'code', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name') },
      { key: 'address', label: t('inventory.warehouses.address'), render: (w: Warehouse) => w.address ?? '—' },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (w: Warehouse) => <StatusBadge status={w.isActive ? 'Active' : 'Inactive'} label={w.isActive ? t('common.active') : t('common.inactive')} />,
      },
      {
        key: 'actions',
        label: '',
        render: (w: Warehouse) =>
          canManage ? (
            <div className="flex gap-3">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setModalState({ open: true, warehouse: w })}>
                {t('common.edit')}
              </button>
              {w.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(w)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button className="text-xs text-green-600 hover:underline" onClick={() => toggleMutation.mutate(w)}>
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
        <h1 className="text-xl font-semibold">{t('inventory.warehouses.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, warehouse: null })}>
            {t('inventory.warehouses.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={data ?? []} isLoading={isLoading} />

      {modalState.open && (
        <WarehouseModal warehouse={modalState.warehouse} onClose={() => setModalState({ open: false, warehouse: null })} />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('inventory.warehouses.deactivateTitle')}
        message={t('inventory.warehouses.deactivateConfirm', { name: confirmTarget?.name })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
