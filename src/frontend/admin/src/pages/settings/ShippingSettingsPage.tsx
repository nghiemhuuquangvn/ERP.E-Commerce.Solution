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
import { serverMessage } from '@/api/purchasing/procurement.api'
import { ShippingCarriersCard } from './ShippingCarriersCard'
import { CarrierRulesCard } from './CarrierRulesCard'

type Tab = 'system' | 'tenant' | 'carriers' | 'rules'

type SystemFormValues = SystemShippingSettings

function SystemSettingsForm({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['settings', 'shipping', 'system'],
    queryFn: () => shippingSettingsApi.getSystemSettings(),
    staleTime: 30_000,
  })

  const { register, handleSubmit, reset } = useForm<SystemFormValues>({
    defaultValues: { customCarriersAllowed: true, ghtkAvailable: true, vtpAvailable: true, ghnAvailable: true, jntAvailable: true },
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

      <div className="space-y-2 rounded border border-gray-200 p-3">
        <p className="text-xs text-gray-500">{t('settings.shipping.carriersAvailableHint')}</p>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} {...register('ghtkAvailable')} /> GHTK
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} {...register('vtpAvailable')} /> Viettel Post
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} {...register('ghnAvailable')} /> GHN
        </label>
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input type="checkbox" disabled={!canManage} {...register('jntAvailable')} /> J&amp;T Express
        </label>
      </div>

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

type TenantFormValues = Omit<TenantShippingSettings, 'tenantId'>

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
    defaultValues: { defaultShippingFee: 0, freeShippingThreshold: 0, defaultItemWeightGrams: 500 },
  })

  useEffect(() => {
    if (data) reset(data)
  }, [data, reset])

  const mutation = useMutation({
    mutationFn: (values: TenantFormValues) =>
      shippingSettingsApi.updateTenantSettings(tenantId, {
        ...values,
        defaultShippingFee: Number(values.defaultShippingFee),
        freeShippingThreshold: Number(values.freeShippingThreshold),
        defaultItemWeightGrams: Number(values.defaultItemWeightGrams),
      }),
    onSuccess: (result: TenantShippingSettings) => {
      queryClient.setQueryData(['settings', 'shipping', 'tenant', tenantId], result)
    },
  })

  if (!tenantId) return <p className="text-sm text-gray-400">{t('settings.shipping.noTenant')}</p>
  if (isLoading) return <p className="text-sm text-gray-400">{t('common.loading')}</p>

  const text = (name: keyof TenantFormValues, label: string) => (
    <label className="block text-sm text-gray-700">
      {t(label)}
      <input disabled={!canManage} {...register(name)} className="input mt-1 w-full" />
    </label>
  )

  return (
    <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="max-w-xl space-y-4">
      <p className="text-xs text-gray-500">{t('settings.shipping.feeHint')}</p>
      <label className="block text-sm text-gray-700">
        {t('settings.shipping.defaultShippingFee')}
        <input type="number" step="0.01" disabled={!canManage} {...register('defaultShippingFee', { valueAsNumber: true })} className="input mt-1 w-full" />
      </label>
      <label className="block text-sm text-gray-700">
        {t('settings.shipping.freeShippingThreshold')}
        <input type="number" step="0.01" disabled={!canManage} {...register('freeShippingThreshold', { valueAsNumber: true })} className="input mt-1 w-full" />
      </label>

      <div className="space-y-3 rounded border border-gray-200 p-4">
        <div>
          <h3 className="text-sm font-semibold text-gray-900">{t('settings.shipping.sender.title')}</h3>
          <p className="text-xs text-gray-500">{t('settings.shipping.sender.hint')}</p>
        </div>
        <div className="grid grid-cols-2 gap-3">
          {text('senderName', 'settings.shipping.sender.name')}
          {text('senderPhone', 'settings.shipping.sender.phone')}
        </div>
        {text('senderAddress', 'settings.shipping.sender.address')}
        <div className="grid grid-cols-3 gap-3">
          {text('senderWard', 'settings.shipping.sender.ward')}
          {text('senderDistrict', 'settings.shipping.sender.district')}
          {text('senderProvince', 'settings.shipping.sender.province')}
        </div>
        <label className="block text-sm text-gray-700">
          {t('settings.shipping.sender.defaultWeight')}
          <input type="number" min={1} disabled={!canManage} {...register('defaultItemWeightGrams', { valueAsNumber: true })} className="input mt-1 w-full" />
          <span className="text-xs text-gray-400">{t('settings.shipping.sender.defaultWeightHint')}</span>
        </label>
      </div>

      {mutation.isError && <p className="text-xs text-red-600">{serverMessage(mutation.error, t('settings.shipping.failedToSave'))}</p>}

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
  const canManageRules = hasPermission('shipping:carriers:manage')
  const tenantId = user?.tenantId ?? ''

  const [tab, setTab] = useState<Tab>(canViewSystem ? 'system' : 'tenant')

  const TABS: { key: Tab; label: string }[] = [
    ...(canViewSystem ? [{ key: 'system' as Tab, label: t('settings.shipping.systemTab') }] : []),
    { key: 'tenant' as Tab, label: t('settings.shipping.tenantTab') },
    { key: 'carriers' as Tab, label: t('settings.shipping.carriersTab') },
    { key: 'rules' as Tab, label: t('settings.shipping.rulesTab') },
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
      {tab === 'tenant' && <TenantSettingsForm canManage={canManageTenant} tenantId={tenantId} />}
      {tab === 'carriers' && <ShippingCarriersCard tenantId={tenantId} canManage={canManageTenant} />}
      {tab === 'rules' && <CarrierRulesCard canManage={canManageRules} />}
    </div>
  )
}
