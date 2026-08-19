import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { tenantsApi } from '@/api/tenants.api'
import type { Tenant } from '@/api/tenants.api'
import { DataTable } from '@/components/shared/DataTable'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'

interface TenantFormValues {
  tenantCode: string
  tenantName: string
}

function TenantModal({ tenant, onClose }: { tenant: Tenant | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const isEdit = !!tenant
  const { register, handleSubmit, formState: { errors } } = useForm<TenantFormValues>({
    defaultValues: { tenantCode: tenant?.tenantCode ?? '', tenantName: tenant?.tenantName ?? '' },
  })

  const mutation = useMutation({
    mutationFn: async (values: TenantFormValues) => {
      if (isEdit) {
        await tenantsApi.update(tenant!.id, { tenantName: values.tenantName })
        return
      }
      await tenantsApi.create({ tenantCode: values.tenantCode, tenantName: values.tenantName })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'tenants'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card w-full max-w-md p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">
          {isEdit ? t('identity.tenants.editTitle') : t('identity.tenants.addTitle')}
        </h3>
        <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
          {!isEdit && (
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.code')}</label>
              <input className="input" {...register('tenantCode', { required: true })} />
              {errors.tenantCode && <p className="mt-1 text-xs text-red-600">{t('common.code')} is required.</p>}
            </div>
          )}
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('common.name')}</label>
            <input className="input" {...register('tenantName', { required: true })} />
            {errors.tenantName && <p className="mt-1 text-xs text-red-600">{t('common.name')} is required.</p>}
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

export function TenantsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()

  const canCreate = hasPermission('identity:tenants:create')
  const canEdit = hasPermission('identity:tenants:edit')
  const canManage = hasPermission('identity:tenants:manage')

  const [modalState, setModalState] = useState<{ open: boolean; tenant: Tenant | null }>({ open: false, tenant: null })
  const [toggleTarget, setToggleTarget] = useState<Tenant | null>(null)

  const { data: tenants, isLoading } = useQuery({
    queryKey: ['identity', 'tenants'],
    queryFn: () => tenantsApi.list(),
    staleTime: 15_000,
  })

  const toggleMutation = useMutation({
    mutationFn: (tn: Tenant) => (tn.status === 1 ? tenantsApi.deactivate(tn.id) : tenantsApi.activate(tn.id)),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['identity', 'tenants'] })
      setToggleTarget(null)
    },
  })

  const columns = useMemo(
    () => [
      { key: 'tenantCode', label: t('common.code'), className: 'font-mono text-xs' },
      { key: 'tenantName', label: t('common.name') },
      {
        key: 'status',
        label: t('common.status'),
        render: (tn: Tenant) => (
          <StatusBadge
            status={tn.status === 1 ? 'Active' : 'Cancelled'}
            label={tn.status === 1 ? t('common.activate') : t('common.deactivate')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (tn: Tenant) => (
          <div className="flex gap-3">
            {canEdit && (
              <button
                className="text-xs text-primary-600 hover:underline"
                onClick={() => setModalState({ open: true, tenant: tn })}
              >
                {t('common.edit')}
              </button>
            )}
            {canManage && (
              <button
                className={tn.status === 1 ? 'text-xs text-red-600 hover:underline' : 'text-xs text-primary-600 hover:underline'}
                onClick={() => setToggleTarget(tn)}
              >
                {tn.status === 1 ? t('common.deactivate') : t('common.activate')}
              </button>
            )}
          </div>
        ),
      },
    ],
    [canEdit, canManage, t],
  )

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">{t('identity.tenants.title')}</h1>
        {canCreate && (
          <button className="btn-primary" onClick={() => setModalState({ open: true, tenant: null })}>
            {t('identity.tenants.add')}
          </button>
        )}
      </div>

      <DataTable columns={columns} data={tenants ?? []} isLoading={isLoading} />

      {modalState.open && (
        <TenantModal tenant={modalState.tenant} onClose={() => setModalState({ open: false, tenant: null })} />
      )}

      <ConfirmDialog
        open={!!toggleTarget}
        title={toggleTarget?.status === 1 ? t('identity.tenants.deactivateTitle') : t('identity.tenants.activateTitle')}
        message={
          toggleTarget?.status === 1
            ? t('identity.tenants.deactivateConfirm', { name: toggleTarget?.tenantName })
            : t('identity.tenants.activateConfirm', { name: toggleTarget?.tenantName })
        }
        confirmLabel={toggleTarget?.status === 1 ? t('common.deactivate') : t('common.activate')}
        danger={toggleTarget?.status === 1}
        onConfirm={() => toggleTarget && toggleMutation.mutate(toggleTarget)}
        onCancel={() => setToggleTarget(null)}
      />
    </div>
  )
}
