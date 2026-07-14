import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  currencySettingsApi,
  type SystemCurrencySettings,
  type TenantCurrencySettings,
} from '@/api/settings/currency.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  multiCurrencyAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'currency', 'system'],
    queryFn: () => currencySettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { multiCurrencyAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => currencySettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemCurrencySettings) => {
      queryClient.setQueryData(['settings', 'currency', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('multiCurrencyAllowed')} />
        {t('settings.currency.multiCurrencyAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.currency.failedToSave')}</p>}

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
  defaultCurrencyCode: string
  exchangeRateSource: string
  decimalDigits: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'currency', 'tenant', tenantId],
    queryFn: () => currencySettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultCurrencyCode: 'USD', exchangeRateSource: 'Manual', decimalDigits: 2 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      currencySettingsApi.updateTenantSettings(tenantId, {
        defaultCurrencyCode: values.defaultCurrencyCode,
        exchangeRateSource: values.exchangeRateSource,
        decimalDigits: Number(values.decimalDigits),
      }),
    onSuccess: (result: TenantCurrencySettings) => {
      queryClient.setQueryData(['settings', 'currency', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.currency.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.currency.defaultCurrencyCode')}
        <input
          type="text"
          maxLength={3}
          disabled={!canManage}
          {...register('defaultCurrencyCode')}
          className="input mt-1 w-full uppercase"
        />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.currency.exchangeRateSource')}
        <input type="text" disabled={!canManage} {...register('exchangeRateSource')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.currency.decimalDigits')}
        <input
          type="number"
          min={0}
          max={6}
          disabled={!canManage}
          {...register('decimalDigits', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.currency.failedToSave')}</p>}

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

export function CurrencySettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:currency:system:view')
  const canManageSystem = hasPermission('settings:currency:system:manage')
  const canManageTenant = hasPermission('settings:currency:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.currency.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.currency.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.currency.title')}</h1>

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
