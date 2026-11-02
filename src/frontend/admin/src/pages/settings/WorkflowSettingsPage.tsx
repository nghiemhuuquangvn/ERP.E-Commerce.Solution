import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  workflowSettingsApi,
  type SystemWorkflowSettings,
  type TenantWorkflowSettings,
} from '@/api/settings/workflow.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiLevelApprovalAvailable: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('multiLevelApprovalAvailable')} label={t('settings.workflow.multiLevelApprovalAvailable')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.workflow.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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
  const toast = useToast()

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
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.workflow.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('multiLevelApprovalEnabled')} label={t('settings.workflow.multiLevelApprovalEnabled')} />
      <FormField label={t('settings.workflow.defaultApprovalLevels')}>
        <Input
          type="number"
          min={1}
          disabled={!canManage}
          {...register('defaultApprovalLevels', { valueAsNumber: true })}
 />
      </FormField>
      <FormField label={t('settings.workflow.autoApproveThresholdAmount')} hint={t('settings.workflow.autoApproveThresholdHint')}>
        <Input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('autoApproveThresholdAmount', { valueAsNumber: true })}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.workflow.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
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

      <TabList id="workflowsettings" label={t('settings.workflow.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
