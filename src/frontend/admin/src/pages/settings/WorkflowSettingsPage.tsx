import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  workflowSettingsApi,
  type SystemWorkflowSettings,
  type TenantWorkflowSettings,
} from '@/api/settings/workflow.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiLevelApprovalAvailable: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'workflow', 'system'],
    queryFn: () => workflowSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { multiLevelApprovalAvailable: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => workflowSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemWorkflowSettings) => {
      queryClient.setQueryData(['settings', 'workflow', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('multiLevelApprovalAvailable')} />
        {t('settings.workflow.multiLevelApprovalAvailable')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.workflow.failedToSave')}</p>}

      {canManage && (
        <div className="pt-2">
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  multiLevelApprovalEnabled: boolean
  defaultApprovalLevels: number
  autoApproveThresholdAmount: number | null
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'workflow', 'tenant', tenantId],
    queryFn: () => workflowSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { multiLevelApprovalEnabled: false, defaultApprovalLevels: 1, autoApproveThresholdAmount: null },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      workflowSettingsApi.updateTenantSettings(tenantId, {
        ...values,
        autoApproveThresholdAmount:
          values.autoApproveThresholdAmount === null || Number.isNaN(values.autoApproveThresholdAmount)
            ? null
            : values.autoApproveThresholdAmount,
      }),
    onSuccess: (result: TenantWorkflowSettings) => {
      queryClient.setQueryData(['settings', 'workflow', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.workflow.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('multiLevelApprovalEnabled')} />
        {t('settings.workflow.multiLevelApprovalEnabled')}
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.workflow.defaultApprovalLevels')}
        <input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('defaultApprovalLevels', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.workflow.autoApproveThresholdAmount')}
        <input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('autoApproveThresholdAmount', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
        <span className="text-xs text-gray-400">{t('settings.workflow.autoApproveThresholdHint')}</span>
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.workflow.failedToSave')}</p>}

      {canManage && (
        <div className="pt-2">
          <button type="submit" disabled={mutation.isPending} className="btn-primary">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      )}
    </form>
  )
}

export function WorkflowSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:workflow:system:view')
  const canManageSystem = hasPermission('settings:workflow:system:manage')
  const canManageTenant = hasPermission('settings:workflow:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.workflow.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.workflow.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.workflow.title')}</h1>

      <div className="flex gap-2 border-b border-gray-200">
        {TABS.map((x) => (
          <button
            key={x.key}
            onClick={() => setTab(x.key)}
            className={`px-3 py-2 text-sm font-medium ${
              tab === x.key
                ? 'border-b-2 border-primary-600 text-primary-700'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {x.label}
          </button>
        ))}
      </div>

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
