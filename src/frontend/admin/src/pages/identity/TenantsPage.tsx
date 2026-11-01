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
import { Alert, Button, EmptyState, FormField, Input, Modal, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'

interface TenantFormValues {
  tenantCode: string
  tenantName: string
}

function TenantModal({ tenant, onClose }: { tenant: Tenant | null; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
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
      toast.success(t('common.saved'))
      onClose()
    },
  })

  return (
    <Modal
      open
      size="sm"
      title={isEdit ? t('identity.tenants.editTitle') : t('identity.tenants.addTitle')}
      onClose={onClose}
      closeOnEsc={!mutation.isPending}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
          <Button type="submit" form="tenant-form" loading={mutation.isPending}>{t('common.save')}</Button>
        </>
      }
    >
      <form id="tenant-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
        {!isEdit && (
          <FormField
            label={t('common.code')}
            required
            hint={t('identity.tenants.codeHint')}
            error={errors.tenantCode ? t('common.required') : undefined}
          >
            <Input {...register('tenantCode', { required: true })} />
          </FormField>
        )}
        <FormField label={t('common.name')} required error={errors.tenantName ? t('common.required') : undefined}>
          <Input {...register('tenantName', { required: true })} />
        </FormField>
        {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('common.failedToSave'))}</Alert>}
      </form>
    </Modal>
  )
}

export function TenantsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
      setToggleTarget(null)
    },
    onError: (e) => {
      setToggleTarget(null)
      toast.error(serverMessage(e, t('common.failedToSave')))
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
            status={tn.status === 1 ? 'Active' : 'Inactive'}
            label={tn.status === 1 ? t('common.active') : t('common.inactive')}
          />
        ),
      },
      {
        key: 'actions',
        label: '',
        render: (tn: Tenant) => (
          <div className="flex gap-3">
            {canEdit && (
              <Button
                variant="link"
                className="text-xs"
                aria-label={`${t('common.edit')} ${tn.tenantName}`}
                onClick={() => setModalState({ open: true, tenant: tn })}
              >
                {t('common.edit')}
              </Button>
            )}
            {canManage && (
              <Button
                variant={tn.status === 1 ? 'link-danger' : 'link'}
                className="text-xs"
                aria-label={`${tn.status === 1 ? t('common.deactivate') : t('common.activate')} ${tn.tenantName}`}
                onClick={() => setToggleTarget(tn)}
              >
                {tn.status === 1 ? t('common.deactivate') : t('common.activate')}
              </Button>
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
        {canCreate && <Button onClick={() => setModalState({ open: true, tenant: null })}>{t('identity.tenants.add')}</Button>}
      </div>

      <DataTable
        columns={columns}
        data={tenants ?? []}
        isLoading={isLoading}
        emptyState={<EmptyState title={t('identity.tenants.empty')} />}
      />

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
