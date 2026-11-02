import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Checkbox, FormField, Input, useToast } from '@/components/ui'
import {
  smsSettingsApi,
  type SystemSmsSettings,
  type TenantSmsSettings,
} from '@/api/settings/sms.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { TabList } from '@/components/shared/PageTabs'
import { serverMessage } from '@/lib/serverMessage'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customSmsGatewayAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'sms', 'system'],
    queryFn: () => smsSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customSmsGatewayAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => smsSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemSmsSettings) => {
      queryClient.setQueryData(['settings', 'sms', 'system'], result)
      toast.success(t('common.saved'))
    },
  })

  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <Checkbox disabled={!canManage} {...register('customSmsGatewayAllowed')} label={t('settings.sms.customSmsGatewayAllowed')} />

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.sms.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

interface TenantFormValues {
  providerName: string
  apiKey: string
  fromNumber: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'sms', 'tenant', tenantId],
    queryFn: () => smsSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { providerName: '', apiKey: '', fromNumber: '' },
  })

  useEffect(() => {
    if (data) {
      reset({ providerName: data.providerName ?? '', apiKey: '', fromNumber: data.fromNumber ?? '' })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      smsSettingsApi.updateTenantSettings(tenantId, {
        providerName: values.providerName,
        // Empty means "leave the currently stored key unchanged" — never send a blank overwrite.
        apiKey: values.apiKey || undefined,
        fromNumber: values.fromNumber,
      }),
    onSuccess: (result: TenantSmsSettings) => {
      queryClient.setQueryData(['settings', 'sms', 'tenant', tenantId], result)
      toast.success(t('common.saved'))
    },
  })

  if (!tenantId) return <p className="text-sm text-ink-muted">{t('settings.sms.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-ink-muted">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <FormField label={t('settings.sms.providerName')}>
        <Input type="text" disabled={!canManage} {...register('providerName')} />
      </FormField>
      <FormField label={t('settings.sms.apiKey')} hint={t('settings.sms.apiKeyHint')}>
        <Input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasApiKey ? t('settings.sms.apiKeyPlaceholderSet') : t('settings.sms.apiKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('apiKey')}
 />
      </FormField>
      <FormField label={t('settings.sms.fromNumber')}>
        <Input type="text" disabled={!canManage} {...register('fromNumber')} />
      </FormField>

      {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('settings.sms.failedToSave'))}</Alert>}

      {canManage && (
        <div className="pt-2">
          <Button type="submit" loading={mutation.isPending}>{t('common.save')}</Button>
        </div>
      )}
    </form>
  )
}

export function SmsSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:sms:system:view')
  const canManageSystem = hasPermission('settings:sms:system:manage')
  const canManageTenant = hasPermission('settings:sms:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.sms.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.sms.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.sms.title')}</h1>

      <TabList id="smssettings" label={t('settings.sms.title')} tabs={TABS} current={tab} onSelect={setTab} />

      {tab === 'system' && canViewSystem && <SystemSettingsForm canManage={canManageSystem} />}
      {tab === 'tenant' && (
        <TenantSettingsForm canManage={canManageTenant} tenantId={user?.tenantId ?? ''} />
      )}
    </div>
  )
}
