import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { locationsApi } from '@/api/locations.api'
import type { WarehouseLocation } from '@/api/locations.api'
import { warehousesApi } from '@/api/warehouses.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface LocationFormValues {
  warehouseId: string
  locationCode: string
  name: string
  zone: string
  aisle: string
  bin: string
  shelf: string
  capacity: number
}

function LocationModal({ location, onClose }: { location: WarehouseLocation | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!location

  const { data: warehouses } = useQuery({ queryKey: ['warehouses'], queryFn: warehousesApi.list, staleTime: 60_000 })

  const { register, handleSubmit, formState: { errors } } = useForm<LocationFormValues>({
    defaultValues: {
      warehouseId: location?.warehouseId ?? '',
      locationCode: location?.locationCode ?? '',
      name: location?.name ?? '',
      zone: location?.zone ?? '',
      aisle: location?.aisle ?? '',
      bin: location?.bin ?? '',
      shelf: location?.shelf ?? '',
      capacity: location?.capacity ?? 1000,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: LocationFormValues) => {
      const payload = {
        warehouseId: values.warehouseId || undefined,
        name: values.name || undefined,
        zone: values.zone || undefined,
        aisle: values.aisle,
        bin: values.bin,
        shelf: values.shelf,
        capacity: Number(values.capacity),
      }
      return isEdit
        ? locationsApi.update(location!.locationId, payload)
        : locationsApi.create({ ...payload, locationCode: values.locationCode })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['locations'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-lg p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('inventory.locations.editTitle') : t('inventory.locations.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            {!isEdit && (
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
                <input className="input" {...register('locationCode', { required: true })} />
                {errors.locationCode && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
              </div>
            )}
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.warehouses.title')}</label>
              <select className="input" {...register('warehouseId')}>
                <option value="">{t('common.select')}</option>
                {(warehouses ?? []).map((w) => (
                  <option key={w.id} value={w.id}>{w.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
              <input className="input" {...register('name')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.locations.zone')}</label>
              <input className="input" {...register('zone')} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.locations.aisle')}</label>
              <input className="input" {...register('aisle', { required: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.locations.bin')}</label>
              <input className="input" {...register('bin', { required: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.locations.shelf')}</label>
              <input className="input" {...register('shelf', { required: true })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('inventory.locations.capacity')}</label>
              <input type="number" className="input" {...register('capacity', { required: true, valueAsNumber: true })} />
            </div>
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

export function LocationsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [modalState, setModalState] = useState<{ open: boolean; location: WarehouseLocation | null }>({ open: false, location: null })
  const [confirmTarget, setConfirmTarget] = useState<WarehouseLocation | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['locations'],
    queryFn: () => locationsApi.list(),
    staleTime: 30_000,
  })

  const canManage = hasPermission('inventory:manage')

  const toggleMutation = useMutation({
    mutationFn: (l: WarehouseLocation) => (l.isActive ? locationsApi.deactivate(l.locationId) : locationsApi.activate(l.locationId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['locations'] })
      setConfirmTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'locationCode', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'name', label: t('common.name'), render: (l: WarehouseLocation) => l.name ?? '—' },
      { key: 'zone', label: t('inventory.locations.zone'), render: (l: WarehouseLocation) => l.zone ?? '—' },
      { key: 'aisle', label: t('inventory.locations.aisle') },
      { key: 'bin', label: t('inventory.locations.bin') },
      { key: 'shelf', label: t('inventory.locations.shelf') },
      { key: 'currentOccupancy', label: `${t('inventory.locations.capacity')}`, render: (l: WarehouseLocation) => `${l.currentOccupancy} / ${l.capacity}` },
      {
        key: 'isActive',
        label: t('common.status'),
        render: (l: WarehouseLocation) => <StatusBadge status={l.isActive ? 'Active' : 'Inactive'} label={l.isActive ? t('common.active') : t('common.inactive')} />,
      },
      {
        key: 'actions',
        label: '',
        render: (l: WarehouseLocation) =>
          canManage ? (
            <div className="flex gap-3">
              <button className="text-xs text-primary-600 hover:underline" onClick={() => setModalState({ open: true, location: l })}>
                {t('common.edit')}
              </button>
              {l.isActive ? (
                <button className="text-xs text-red-600 hover:underline" onClick={() => setConfirmTarget(l)}>
                  {t('common.deactivate')}
                </button>
              ) : (
                <button className="text-xs text-green-600 hover:underline" onClick={() => toggleMutation.mutate(l)}>
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
        <h1 className="text-xl font-semibold">{t('inventory.locations.title')}</h1>
        {canManage && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, location: null })}>
            {t('inventory.locations.add')}
          </button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={(data ?? []).map((l) => ({ ...l, id: l.locationId }))}
        isLoading={isLoading}
      />

      {modalState.open && (
        <LocationModal location={modalState.location} onClose={() => setModalState({ open: false, location: null })} />
      )}

      <ConfirmDialog
        open={!!confirmTarget}
        title={t('inventory.locations.deactivateTitle')}
        message={t('inventory.locations.deactivateConfirm', { name: confirmTarget?.locationCode })}
        confirmLabel={t('common.deactivate')}
        danger
        onConfirm={() => confirmTarget && toggleMutation.mutate(confirmTarget)}
        onCancel={() => setConfirmTarget(null)}
      />
    </div>
  )
}
