import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  advancedSettingsApi,
  type SystemAdvancedSettings,
  type TenantAdvancedSettings,
} from '@/api/settings/advanced.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  debugModeAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'advanced', 'system'],
    queryFn: () => advancedSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { debugModeAllowed: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => advancedSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemAdvancedSettings) => {
      queryClient.setQueryData(['settings', 'advanced', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('debugModeAllowed')} label={t('settings.advanced.debugModeAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.advanced.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  debugModeEnabled: boolean
  customCssUrl: string
  dataExportEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'advanced', 'tenant', tenantId],
    queryFn: () => advancedSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { debugModeEnabled: false, customCssUrl: '', dataExportEnabled: true },
  })

  useEffect(() => {
    if (data) {
      reset({ debugModeEnabled: data.debugModeEnabled, customCssUrl: data.customCssUrl ?? '', dataExportEnabled: data.dataExportEnabled })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      advancedSettingsApi.updateTenantSettings(tenantId, {
        debugModeEnabled: values.debugModeEnabled,
        customCssUrl: values.customCssUrl || null,
        dataExportEnabled: values.dataExportEnabled,
      }),
    onSuccess: (result: TenantAdvancedSettings) => {
      queryClient.setQueryData(['settings', 'advanced', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.advanced.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('debugModeEnabled')} label={t('settings.advanced.debugModeEnabled')} />
      <FormField label={t('settings.advanced.customCssUrl')}>
        <Input type="text" disabled={!canManage} {...register('customCssUrl')} />
      </FormField>
      <Checkbox disabled={!canManage} {...register('dataExportEnabled')} label={t('settings.advanced.dataExportEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.advanced.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function AdvancedSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:advanced:system:view')
  const canManageSystem = hasPermission('settings:advanced:system:manage')
  const canManageTenant = hasPermission('settings:advanced:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.advanced.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.advanced.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.advanced.title')}</h1>

      <TabList id="advancedsettings" label={t('settings.advanced.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
