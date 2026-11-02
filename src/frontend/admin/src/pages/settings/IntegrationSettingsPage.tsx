import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  integrationSettingsApi,
  type SystemIntegrationSettings,
  type TenantIntegrationSettings,
} from '@/api/settings/integration.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  externalIntegrationsAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'integration', 'system'],
    queryFn: () => integrationSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { externalIntegrationsAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => integrationSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemIntegrationSettings) => {
      queryClient.setQueryData(['settings', 'integration', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('externalIntegrationsAllowed')} label={t('settings.integration.externalIntegrationsAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.integration.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  externalApiBaseUrl: string
  externalApiKey: string
  syncEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'integration', 'tenant', tenantId],
    queryFn: () => integrationSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { externalApiBaseUrl: '', externalApiKey: '', syncEnabled: false },
  })

  useEffect(() => {
    if (data) {
      reset({ externalApiBaseUrl: data.externalApiBaseUrl ?? '', externalApiKey: '', syncEnabled: data.syncEnabled })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      integrationSettingsApi.updateTenantSettings(tenantId, {
        externalApiBaseUrl: values.externalApiBaseUrl || null,
        // Empty means "leave the currently stored key unchanged" — never send a blank overwrite.
        externalApiKey: values.externalApiKey || undefined,
        syncEnabled: values.syncEnabled,
      }),
    onSuccess: (result: TenantIntegrationSettings) => {
      queryClient.setQueryData(['settings', 'integration', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.integration.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.integration.externalApiBaseUrl')}>
        <Input type="text" disabled={!canManage} {...register('externalApiBaseUrl')} />
      </FormField>
      <FormField label={t('settings.integration.externalApiKey')} hint={t('settings.integration.apiKeyHint')}>
        <Input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasExternalApiKey ? t('settings.integration.apiKeyPlaceholderSet') : t('settings.integration.apiKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('externalApiKey')}
 />
      </FormField>
      <Checkbox disabled={!canManage} {...register('syncEnabled')} label={t('settings.integration.syncEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.integration.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function IntegrationSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:integration:system:view')
  const canManageSystem = hasPermission('settings:integration:system:manage')
  const canManageTenant = hasPermission('settings:integration:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.integration.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.integration.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.integration.title')}</h1>

      <TabList id="integrationsettings" label={t('settings.integration.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
