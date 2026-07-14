import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  smsSettingsApi,
  type SystemSmsSettings,
  type TenantSmsSettings,
} from '@/api/settings/sms.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customSmsGatewayAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('customSmsGatewayAllowed')} />
        {t('settings.sms.customSmsGatewayAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.sms.failedToSave')}</p>}

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
  providerName: string
  apiKey: string
  fromNumber: string
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

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
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.sms.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.sms.providerName')}
        <input type="text" disabled={!canManage} {...register('providerName')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.sms.apiKey')}
        <input
          type="password"
          disabled={!canManage}
          placeholder={data?.hasApiKey ? t('settings.sms.apiKeyPlaceholderSet') : t('settings.sms.apiKeyPlaceholderUnset')}
          autoComplete="new-password"
          {...register('apiKey')}
          className="input mt-1 w-full"
        />
        <span className="text-xs text-gray-400">{t('settings.sms.apiKeyHint')}</span>
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.sms.fromNumber')}
        <input type="text" disabled={!canManage} {...register('fromNumber')} className="input mt-1 w-full" />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.sms.failedToSave')}</p>}

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
