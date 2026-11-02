import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  generalSettingsApi,
  type SystemGeneralSettings,
  type TenantGeneralSettings,
} from '@/api/settings/general.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  maintenanceModeEnabled: boolean
  defaultLanguageCode: string
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'general', 'system'],
    queryFn: () => generalSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { maintenanceModeEnabled: false, defaultLanguageCode: 'en' },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => generalSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemGeneralSettings) => {
      queryClient.setQueryData(['settings', 'general', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('maintenanceModeEnabled')} label={t('settings.general.maintenanceModeEnabled')} />
      <FormField label={t('settings.general.defaultLanguageCode')}>
        <Input
          type="text"
          disabled={!canManage}
          {...register('defaultLanguageCode')}
 />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.general.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  siteName: string
  defaultLanguageCode: string
  timeZone: string
  dateFormat: string
  maintenanceModeEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'general', 'tenant', tenantId],
    queryFn: () => generalSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: {
      siteName: '',
      defaultLanguageCode: 'en',
      timeZone: 'UTC',
      dateFormat: 'yyyy-MM-dd',
      maintenanceModeEnabled: false,
    },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => generalSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantGeneralSettings) => {
      queryClient.setQueryData(['settings', 'general', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.general.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.general.siteName')}>
        <Input type="text" disabled={!canManage} {...register('siteName')} />
      </FormField>
      <FormField label={t('settings.general.defaultLanguageCode')}>
        <Input
          type="text"
          disabled={!canManage}
          {...register('defaultLanguageCode')}
 />
      </FormField>
      <FormField label={t('settings.general.timeZone')}>
        <Input type="text" disabled={!canManage} {...register('timeZone')} />
      </FormField>
      <FormField label={t('settings.general.dateFormat')}>
        <Input type="text" disabled={!canManage} {...register('dateFormat')} />
      </FormField>
      <Checkbox disabled={!canManage} {...register('maintenanceModeEnabled')} label={t('settings.general.maintenanceModeEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.general.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function GeneralSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:general:system:view')
  const canManageSystem = hasPermission('settings:general:system:manage')
  const canManageTenant = hasPermission('settings:general:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.general.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.general.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.general.title')}</h1>

      <TabList id="generalsettings" label={t('settings.general.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
