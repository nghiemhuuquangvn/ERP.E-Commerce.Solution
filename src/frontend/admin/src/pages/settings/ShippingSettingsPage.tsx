import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  shippingSettingsApi,
  type SystemShippingSettings,
  type TenantShippingSettings,
} from '@/api/settings/shipping.settings.api'
import { usePermission } from '@/hooks/usePermission'
import { useAuthStore } from '@/stores/auth.store'

type Tab = 'system' | 'tenant'

interface SystemFormValues {
  customCarriersAllowed: boolean
}

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'system'],
    queryFn: () => shippingSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customCarriersAllowed: true },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: SystemFormValues) => shippingSettingsApi.updateSystemSettings(values),
    onSuccess: (result: SystemShippingSettings) => {
      queryClient.setQueryData(['settings', 'shipping', 'system'], result)
    },
  })

  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input type="checkbox" disabled={!canManage} {...register('customCarriersAllowed')} />
        {t('settings.shipping.customCarriersAllowed')}
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.shipping.failedToSave')}</p>}

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
  defaultShippingFee: number
  freeShippingThreshold: number
}

function TenantSettingsForm({ canManage, tenantId }: { canManage: boolean; tenantId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'tenant', tenantId],
    queryFn: () => shippingSettingsApi.getTenantSettings(tenantId),
    enabled: !!tenantId,
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<TenantFormValues>({
    defaultValues: { defaultShippingFee: 0, freeShippingThreshold: 0 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      shippingSettingsApi.updateTenantSettings(tenantId, {
        defaultShippingFee: Number(values.defaultShippingFee),
        freeShippingThreshold: Number(values.freeShippingThreshold),
      }),
    onSuccess: (result: TenantShippingSettings) => {
      queryClient.setQueryData(['settings', 'shipping', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.shipping.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-md space-y-4">
      <label className="block text-sm text-gray-700">
        {t('settings.shipping.defaultShippingFee')}
        <input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('defaultShippingFee', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.shipping.freeShippingThreshold')}
        <input
          type="number"
          step="0.01"
          disabled={!canManage}
          {...register('freeShippingThreshold', { valueAsNumber: true })}
          className="input mt-1 w-full"
        />
      </label>

      {mutation.isError && <p className="text-xs text-red-600">{t('settings.shipping.failedToSave')}</p>}

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

export function ShippingSettingsPage() {
  const { t } = useTranslation()
  const { hasPermission } = usePermission()
  const user = useAuthStore((s) => s.user)

  const canViewSystem = hasPermission('settings:shipping:system:view')
  const canManageSystem = hasPermission('settings:shipping:system:manage')
  const canManageTenant = hasPermission('settings:shipping:tenant:manage')

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.shipping.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.shipping.tenantTab') },
  ]

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">{t('settings.shipping.title')}</h1>

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
