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
import { Alert, Button, EmptyState, FormField, Input, Modal, Select, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

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
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="md"
      title={isEdit ? t('inventory.locations.editTitle') : t('inventory.locations.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="location-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="location-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          {!isEdit && (
            <FormField label={t('common.code')} required error={errors.locationCode ? t('common.required') : undefined}>
              <Input {...register('locationCode', { required: true })} />
            </FormField>
          )}
          <FormField label={t('inventory.locations.warehouse')}>
            <Select {...register('warehouseId')}>
              <option value="">{t('common.select')}</option>
              {(warehouses ?? []).map((w) => (
                <option key={w.id} value={w.id}>{w.name}</option>
              ))}
            </Select>
          </FormField>
          <FormField label={t('common.name')}>
            <Input {...register('name')} />
          </FormField>
          <FormField label={t('inventory.locations.zone')}>
            <Input {...register('zone')} />
          </FormField>
          <FormField label={t('inventory.locations.aisle')} required error={errors.aisle ? t('common.required') : undefined}>
            <Input {...register('aisle', { required: true })} />
          </FormField>
          <FormField label={t('inventory.locations.bin')} required error={errors.bin ? t('common.required') : undefined}>
            <Input {...register('bin', { required: true })} />
          </FormField>
          <FormField label={t('inventory.locations.shelf')} required error={errors.shelf ? t('common.required') : undefined}>
            <Input {...register('shelf', { required: true })} />
          </FormField>
          <FormField label={t('inventory.locations.capacity')} required error={errors.capacity ? t('common.required') : undefined}>
            <Input type="number" step="any" {...register('capacity', { required: true, valueAsNumber: true })} />
          </FormField>
        </div>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function LocationsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [warehouseFilter, setWarehouseFilter] = useState('')
  const [modalState, setModalState] = useState<{ open: boolean; location: WarehouseLocation | null }>({ open: false, location: null })
  const [confirmTarget, setConfirmTarget] = useState<WarehouseLocation | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['locations'],
    queryFn: () => locationsApi.list(),
    staleTime: 30_000,
  })

  const { data: warehouses } = useQuery({ queryKey: ['warehouses'], queryFn: warehousesApi.list, staleTime: 60_000 })
  const canManage = hasPermission('inventory:manage')
  const rows = (data ?? []).filter((l) => !warehouseFilter || l.warehouseId === warehouseFilter).map((l) => ({ ...l, id: l.locationId }))

  const toggleMutation = useMutation({
    mutationFn: (l: WarehouseLocation) => (l.isActive ? locationsApi.deactivate(l.locationId) : locationsApi.activate(l.locationId)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['locations'] })
      toast.success(t('common.saved'))
      setConfirmTarget(null)
    },
    onError: (e) => {
      setConfirmTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
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
      {
        key: 'currentOccupancy',
        label: t('inventory.locations.capacity'),
        align: 'right' as const,
        render: (l: WarehouseLocation) => {
          const pct = l.capacity > 0 ? Math.min(100, Math.round((l.currentOccupancy / l.capacity) * 100)) : 0
          return (
            <div className="ml-auto w-32">
              <div className="text-right text-xs tabular-nums">{l.currentOccupancy} / {l.capacity}</div>
              <div className="mt-1 h-1.5 rounded-full bg-surface-muted" role="img" aria-label={`${pct}%`}>
                <div className={`h-1.5 rounded-full ${pct >= 90 ? 'bg-danger' : 'bg-brand-action'}`} style={{ width: `${pct}%` }} />
              </div>
            </div>
          )
        },
      },
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
              <Button variant="link" className="text-xs" onClick={() => setModalState({ open: true, location: l })}>
                {t('common.edit')}
              </Button>
              {l.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(l)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs" onClick={() => toggleMutation.mutate(l)}>
                  {t('common.activate')}
                </Button>
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
          <Button onClick={() => setModalState({ open: true, location: null })}>
            {t('inventory.locations.add')}
          </Button>
        )}
      </div>

      <Select
        className="max-w-xs"
        aria-label={t('inventory.warehouses.title')}
        value={warehouseFilter}
        onChange={(e) => setWarehouseFilter(e.target.value)}
      >
        <option value="">{t('common.all')}</option>
        {(warehouses ?? []).map((w) => (
          <option key={w.id} value={w.id}>{w.name}</option>
        ))}
      </Select>

      <DataTable
        columns={columns}
        data={rows}
        isLoading={isLoading}
        emptyState={
          warehouseFilter ? (
            <EmptyState
              variant="no-results"
              title={t('common.noResults')}
              description={t('common.noResultsHint')}
              action={<Button variant="outline" onClick={() => setWarehouseFilter('')}>{t('common.clear')}</Button>}
            />
          ) : undefined
        }
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
