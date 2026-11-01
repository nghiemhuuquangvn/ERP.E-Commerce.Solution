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
import { Alert, Button, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface WarehouseFormValues {
  code: string
  name: string
  address: string
}

function WarehouseModal({ warehouse, onClose }: { warehouse: Warehouse | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('inventory.warehouses.editTitle') : t('inventory.warehouses.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="warehouse-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="warehouse-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField label={t('common.code')} required error={errors.code ? t('common.required') : undefined}>
            <Input {...register('code', { required: true })} />
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.name ? t('common.required') : undefined}>
          <Input {...register('name', { required: true })} />
        </FormField>
        <FormField label={t('inventory.warehouses.address')}>
          <Input {...register('address')} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function WarehousesPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()
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
              <Button variant="link" className="text-xs" onClick={() => setModalState({ open: true, warehouse: w })}>
                {t('common.edit')}
              </Button>
              {w.isActive ? (
                <Button variant="link-danger" className="text-xs" onClick={() => setConfirmTarget(w)}>
                  {t('common.deactivate')}
                </Button>
              ) : (
                <Button variant="link" className="text-xs" onClick={() => toggleMutation.mutate(w)}>
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
        <h1 className="text-xl font-semibold">{t('inventory.warehouses.title')}</h1>
        {canManage && (
          <Button onClick={() => setModalState({ open: true, warehouse: null })}>
            {t('inventory.warehouses.add')}
          </Button>
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
