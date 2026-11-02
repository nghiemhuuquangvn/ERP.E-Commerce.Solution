import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  localizationSettingsApi,
  type SystemLocalizationSettings,
  type TenantLocalizationSettings,
} from '@/api/settings/localization.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiLanguageAllowed: boolean
  defaultLocale: string
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'localization', 'system'],
    queryFn: () => localizationSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { multiLanguageAllowed: true, defaultLocale: 'en-US' },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => localizationSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemLocalizationSettings) => {
      queryClient.setQueryData(['settings', 'localization', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('multiLanguageAllowed')} label={t('settings.localization.multiLanguageAllowed')} />
      <FormField label={t('settings.localization.defaultLocale')}>
        <Input type="text" disabled={!canManage} {...register('defaultLocale')} />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.localization.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  defaultLocale: string
  supportedLocales: string
  numberFormatLocale: string
  rtlEnabled: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'localization', 'tenant', tenantId],
    queryFn: () => localizationSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultLocale: 'en-US', supportedLocales: 'en-US', numberFormatLocale: 'en-US', rtlEnabled: false },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) => localizationSettingsApi.updateTenantSettings(tenantId, values),
    onSuccess: (result: TenantLocalizationSettings) => {
      queryClient.setQueryData(['settings', 'localization', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.localization.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.localization.defaultLocale')}>
        <Input type="text" disabled={!canManage} {...register('defaultLocale')} />
      </FormField>
      <FormField label={t('settings.localization.supportedLocales')} hint={t('settings.localization.supportedLocalesHint')}>
        <Input type="text" disabled={!canManage} {...register('supportedLocales')} />
      </FormField>
      <FormField label={t('settings.localization.numberFormatLocale')}>
        <Input type="text" disabled={!canManage} {...register('numberFormatLocale')} />
      </FormField>
      <Checkbox disabled={!canManage} {...register('rtlEnabled')} label={t('settings.localization.rtlEnabled')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.localization.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function LocalizationSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:localization:system:view')
  const canManageSystem = hasPermission('settings:localization:system:manage')
  const canManageTenant = hasPermission('settings:localization:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.localization.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.localization.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.localization.title')}</h1>

      <TabList id="localizationsettings" label={t('settings.localization.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
