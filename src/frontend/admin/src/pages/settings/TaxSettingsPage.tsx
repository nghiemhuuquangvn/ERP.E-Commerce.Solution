import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  taxSettingsApi,
  type SystemTaxSettings,
  type TenantTaxSettings,
} from '@/api/settings/tax.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  regionalTaxConfigAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tax', 'system'],
    queryFn: () => taxSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { regionalTaxConfigAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => taxSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemTaxSettings) => {
      queryClient.setQueryData(['settings', 'tax', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('regionalTaxConfigAllowed')} />
        {t('settings.tax.regionalTaxConfigAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.tax.failedToSave')}</p>}

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
  companyTaxCode: string
  defaultTaxRatePercent: number
  pricesIncludeTax: boolean
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'tax', 'tenant', tenantId],
    queryFn: () => taxSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { companyTaxCode: '', defaultTaxRatePercent: 0, pricesIncludeTax: true },
  })

  useEffect(() => {
    if (data) {
      reset({
        companyTaxCode: data.companyTaxCode ?? '',
        defaultTaxRatePercent: data.defaultTaxRatePercent,
        pricesIncludeTax: data.pricesIncludeTax,
      })
    }
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      taxSettingsApi.updateTenantSettings(tenantId, {
        companyTaxCode: values.companyTaxCode || null,
        defaultTaxRatePercent: Number(values.defaultTaxRatePercent),
        pricesIncludeTax: values.pricesIncludeTax,
      }),
    onSuccess: (result: TenantTaxSettings) => {
      queryClient.setQueryData(['settings', 'tax', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.tax.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.tax.companyTaxCode')}
        <input type="text" disabled={!canManage} {...register('companyTaxCode')} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.tax.defaultTaxRatePercent')}
        <input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('defaultTaxRatePercent', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('pricesIncludeTax')} />
        {t('settings.tax.pricesIncludeTax')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.tax.failedToSave')}</p>}

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

export function TaxSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:tax:system:view')
  const canManageSystem = hasPermission('settings:tax:system:manage')
  const canManageTenant = hasPermission('settings:tax:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.tax.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.tax.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.tax.title')}</h1>

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
